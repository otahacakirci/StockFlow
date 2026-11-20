using Microsoft.EntityFrameworkCore;

namespace StockFlow.Tests.Infrastructure;

public sealed class SqlServerTestDatabaseSmokeTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task InitializeAsync_AppliesAllMigrationsWithoutPendingChanges()
    {
        await using var dbContext = CreateDbContext();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        Assert.Contains("20260818105705_InitialDomainSchema", appliedMigrations);
        Assert.Contains("20260824065853_AddIdentitySchema", appliedMigrations);
        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task InitializeAsync_UsesIsolatedDatabaseOnTestContainer()
    {
        await using var dbContext = CreateDbContext();

        var currentDatabaseName = await dbContext.Database
            .SqlQueryRaw<string>("SELECT DB_NAME() AS [Value]")
            .SingleAsync();
        var isLocalDb = await dbContext.Database
            .SqlQueryRaw<int>("SELECT CAST(ISNULL(SERVERPROPERTY('IsLocalDB'), 0) AS int) AS [Value]")
            .SingleAsync();

        Assert.Equal(DatabaseName, currentDatabaseName);
        Assert.Equal(0, isLocalDb);
    }
}
