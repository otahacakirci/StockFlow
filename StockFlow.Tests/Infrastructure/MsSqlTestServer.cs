using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace StockFlow.Tests.Infrastructure;

internal static class MsSqlTestServer
{
    internal const string Image = "mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04";
    internal const string AdministratorUserId = "sa";

    private static readonly Lazy<Task<MsSqlTestServerEndpoint>> Endpoint = new(
        StartAsync,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static MsSqlContainer? _container;

    internal static Task<MsSqlTestServerEndpoint> GetAsync()
    {
        return Endpoint.Value;
    }

    internal static InvalidOperationException CreateStartupFailure(Exception inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        return new InvalidOperationException(
            "SQL Server test konteyneri başlatılamadı. Docker'ın kurulu ve çalışır olduğunu doğrulayın; bu bir ortam hatasıdır, testler geçmiş sayılmaz.",
            inner);
    }

    private static async Task<MsSqlTestServerEndpoint> StartAsync()
    {
        MsSqlContainer? container = null;

        try
        {
            var administratorSecret = GenerateAdministratorSecret();

            container = new MsSqlBuilder(Image)
                .WithPassword(administratorSecret)
                .Build();

            await container.StartAsync().ConfigureAwait(false);

            var containerConnection = new SqlConnectionStringBuilder(container.GetConnectionString());
            _container = container;

            return new MsSqlTestServerEndpoint(
                containerConnection.DataSource,
                AdministratorUserId,
                administratorSecret);
        }
        catch (Exception exception)
        {
            if (container is not null)
            {
                try
                {
                    await container.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Temizlik hatası asıl başlatma hatasını gizlememelidir; kaynak toplayıcı konteyneri kaldırır.
                }
            }

            throw CreateStartupFailure(exception);
        }
    }

    private static string GenerateAdministratorSecret()
    {
        // SQL Server karmaşıklık kuralı: büyük harf, küçük harf, rakam ve sembol.
        return string.Concat("Sf1!", Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N").ToUpperInvariant());
    }
}

internal sealed class MsSqlTestServerEndpoint(string dataSource, string userId, string password)
{
    internal string DataSource { get; } = dataSource;

    internal string UserId { get; } = userId;

    internal string Password { get; } = password;
}
