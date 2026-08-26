using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Data;

namespace StockFlow.Tests.Infrastructure;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    internal const string DatabaseNamePrefix = "StockFlow_Tests_";
    internal const string LocalDbDataSource = @"(localdb)\MSSQLLocalDB";

    private readonly string _connectionString;
    private bool _initialized;
    private bool _disposed;

    private SqlServerTestDatabase(string databaseName)
    {
        DatabaseName = databaseName;
        _connectionString = BuildConnectionString(LocalDbDataSource, databaseName);
        EnsureSafeTarget();
    }

    internal string DatabaseName { get; }

    internal static SqlServerTestDatabase Create()
    {
        return new SqlServerTestDatabase(GenerateDatabaseName());
    }

    internal static string GenerateDatabaseName()
    {
        return $"{DatabaseNamePrefix}{Guid.NewGuid():N}";
    }

    internal static void ValidateTarget(string dataSource, string databaseName)
    {
        if (!string.Equals(dataSource, LocalDbDataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Test veritabanı işlemi yalnızca sabit SQL Server LocalDB örneğinde çalışabilir.");
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
        var connectionString = BuildConnectionString(LocalDbDataSource, databaseName);
        ValidateConnectionString(connectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
    }

    internal void AddDbContext(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfDisposed();
        EnsureSafeTarget();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(_connectionString));
    }

    internal ApplicationDbContext CreateDbContext()
    {
        ThrowIfDisposed();
        EnsureSafeTarget();
        return CreateDbContextCore();
    }

    internal async Task InitializeAsync()
    {
        ThrowIfDisposed();
        EnsureSafeTarget();

        if (_initialized)
        {
            return;
        }

        try
        {
            await using var dbContext = CreateDbContextCore();
            await dbContext.Database.MigrateAsync();
            _initialized = true;
        }
        catch (Exception initializationException)
        {
            var cleanupException = await TryDeleteDatabaseAsync();
            if (cleanupException is not null)
            {
                throw new AggregateException(
                    "SQL Server LocalDB test veritabanı başlatılamadı ve güvenli temizlik tamamlanamadı.",
                    initializationException,
                    cleanupException);
            }

            if (initializationException is SqlException)
            {
                throw new InvalidOperationException(
                    "SQL Server LocalDB test veritabanı başlatılamadı. Windows LocalDB kurulumunu ve MSSQLLocalDB örneğini doğrulayın.",
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
            await DeleteDatabaseAsync();
        }
        catch (SqlException exception)
        {
            throw new InvalidOperationException(
                "Geçici SQL Server LocalDB test veritabanı güvenli biçimde silinemedi.",
                exception);
        }
        finally
        {
            _disposed = true;
        }
    }

    private static string BuildConnectionString(string dataSource, string databaseName)
    {
        ValidateTarget(dataSource, databaseName);

        return new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectTimeout = 30,
            ApplicationName = "StockFlow.Tests"
        }.ConnectionString;
    }

    private static void ValidateConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        ValidateTarget(builder.DataSource, builder.InitialCatalog);

        if (!builder.IntegratedSecurity
            || builder.Pooling
            || !string.IsNullOrEmpty(builder.UserID)
            || !string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException(
                "Test veritabanı bağlantısı tümleşik kimlik doğrulaması kullanmalı ve kimlik bilgisi içermemelidir.");
        }
    }

    private void EnsureSafeTarget()
    {
        ValidateConnectionString(_connectionString);
    }

    private ApplicationDbContext CreateDbContextCore()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new ApplicationDbContext(options);
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
