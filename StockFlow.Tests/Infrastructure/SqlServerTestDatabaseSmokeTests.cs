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
}
