using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Data;

namespace StockFlow.Tests.Infrastructure;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    internal const string DatabaseNamePrefix = "StockFlow_Tests_";
    internal const string UninitializedDataSource = "stockflow-tests-uninitialized.invalid";

    private static readonly SemaphoreSlim DatabaseCreationLock = new(1, 1);

    private MsSqlTestServerEndpoint? _endpoint;
    private string? _connectionString;
    private bool _initialized;
    private bool _disposed;

    private SqlServerTestDatabase(string databaseName)
    {
        DatabaseName = databaseName;
    }

    internal string DatabaseName { get; }

    internal string ConnectionString
    {
        get
        {
            ThrowIfDisposed();
            EnsureInitialized();
            EnsureSafeTarget();
            return _connectionString!;
        }
    }

    internal static SqlServerTestDatabase Create()
    {
        return new SqlServerTestDatabase(GenerateDatabaseName());
    }

    internal static string GenerateDatabaseName()
    {
        return $"{DatabaseNamePrefix}{Guid.NewGuid():N}";
    }

    internal static void ValidateTarget(string dataSource, string databaseName, string expectedDataSource)
    {
        if (string.IsNullOrWhiteSpace(expectedDataSource)
            || !string.Equals(dataSource, expectedDataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Test veritabanı işlemi yalnızca bu test koşusunun SQL Server test konteynerinde çalışabilir.");
        }

        if (!databaseName.StartsWith(DatabaseNamePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(databaseName[DatabaseNamePrefix.Length..], "N", out _))
        {
            throw new InvalidOperationException(
                "Test veritabanı işlemi yalnızca benzersiz StockFlow test veritabanlarında çalışabilir.");
        }
    }

    internal static void AddUninitializedDbContext(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var databaseName = GenerateDatabaseName();
        ValidateTarget(UninitializedDataSource, databaseName, UninitializedDataSource);

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = UninitializedDataSource,
            InitialCatalog = databaseName,
            IntegratedSecurity = false,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectTimeout = 30,
            ApplicationName = "StockFlow.Tests"
        }.ConnectionString;

        var builder = new SqlConnectionStringBuilder(connectionString);
        ValidateTarget(builder.DataSource, builder.InitialCatalog, UninitializedDataSource);
        if (builder.IntegratedSecurity
            || builder.Pooling
            || !string.IsNullOrEmpty(builder.UserID)
            || !string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException(
                "Başlatılmamış test veritabanı bağlantısı kimlik bilgisi içermemelidir.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
    }

    internal void AddDbContext(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfDisposed();
        EnsureInitialized();
        EnsureSafeTarget();

        var connectionString = _connectionString!;
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
    }

    internal ApplicationDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        EnsureSafeTarget();
        return CreateDbContextCore(interceptors);
    }

    internal async Task InitializeAsync()
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        if (_endpoint is null)
        {
            var endpoint = await MsSqlTestServer.GetAsync();
            _connectionString = BuildConnectionString(endpoint, DatabaseName);
            _endpoint = endpoint;
        }

        EnsureSafeTarget();

        try
        {
            await using var dbContext = CreateDbContextCore();

            // Tek SQL Server örneğinde eşzamanlı CREATE DATABASE, model veritabanı kilidi
            // nedeniyle (hata 1807) başarısız olabilir; oluşturma sıraya alınır, migration paralel kalır.
            await DatabaseCreationLock.WaitAsync();
            try
            {
                await dbContext.GetService<IRelationalDatabaseCreator>().CreateAsync();
            }
            finally
            {
                DatabaseCreationLock.Release();
            }

            await dbContext.Database.MigrateAsync();
            _initialized = true;
        }
        catch (Exception initializationException)
        {
            var cleanupException = await TryDeleteDatabaseAsync();
            if (cleanupException is not null)
            {
                throw new AggregateException(
                    "SQL Server test veritabanı başlatılamadı ve güvenli temizlik tamamlanamadı.",
                    initializationException,
                    cleanupException);
            }

            if (initializationException is SqlException)
            {
                throw new InvalidOperationException(
                    "SQL Server test veritabanı başlatılamadı. Docker'daki SQL Server test konteynerini doğrulayın; bu bir ortam hatasıdır.",
                    initializationException);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_endpoint is not null)
            {
                await DeleteDatabaseAsync();
            }
        }
        catch (SqlException exception)
        {
            throw new InvalidOperationException(
                "Geçici SQL Server test veritabanı güvenli biçimde silinemedi.",
                exception);
        }
        finally
        {
            _disposed = true;
        }
    }

    private static string BuildConnectionString(MsSqlTestServerEndpoint endpoint, string databaseName)
    {
        ValidateTarget(endpoint.DataSource, databaseName, endpoint.DataSource);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = endpoint.DataSource,
            InitialCatalog = databaseName,
            IntegratedSecurity = false,
            UserID = endpoint.UserId,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectTimeout = 30,
            ApplicationName = "StockFlow.Tests"
        };
        builder.Password = endpoint.Password;

        return builder.ConnectionString;
    }

    private static void ValidateConnectionString(string connectionString, MsSqlTestServerEndpoint endpoint)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        ValidateTarget(builder.DataSource, builder.InitialCatalog, endpoint.DataSource);

        if (builder.IntegratedSecurity
            || builder.Pooling
            || !string.Equals(builder.UserID, endpoint.UserId, StringComparison.Ordinal)
            || !string.Equals(builder.Password, endpoint.Password, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Test veritabanı bağlantısı yalnızca bu test koşusunda üretilen geçici konteyner kimliğini kullanmalı ve bağlantı havuzunu kapatmalıdır.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Test veritabanı başlatılmadı.");
        }
    }

    private void EnsureSafeTarget()
    {
        if (_endpoint is null || _connectionString is null)
        {
            throw new InvalidOperationException("Test veritabanı başlatılmadı.");
        }

        ValidateConnectionString(_connectionString, _endpoint);
    }

    private ApplicationDbContext CreateDbContextCore(params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private async Task DeleteDatabaseAsync()
    {
        EnsureSafeTarget();
        await using var dbContext = CreateDbContextCore();
        await dbContext.Database.EnsureDeletedAsync();
    }

    private async Task<Exception?> TryDeleteDatabaseAsync()
    {
        try
        {
            await DeleteDatabaseAsync();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
