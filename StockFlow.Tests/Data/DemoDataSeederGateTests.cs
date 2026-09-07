using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Options;

namespace StockFlow.Tests.Data;

public sealed class DemoDataSeederGateTests
{
    [Theory]
    [InlineData(false, "Development", nameof(DemoDataSeedOutcome.Disabled))]
    [InlineData(true, "Production", nameof(DemoDataSeedOutcome.NonDevelopmentEnvironment))]
    public async Task SeedAsync_WhenGateIsClosed_ReturnsBeforeDatabaseAccess(
        bool enabled,
        string environmentName,
        string expectedOutcome)
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused")
            .Options;
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var seeder = new DemoDataSeeder(
            dbContext,
            new TestHostEnvironment(environmentName),
            Microsoft.Extensions.Options.Options.Create(
                new DemoDataOptions { Enabled = enabled }),
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions()),
            NullLogger<DemoDataSeeder>.Instance);

        var outcome = await seeder.SeedAsync();

        Assert.Equal(expectedOutcome, outcome.ToString());
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "StockFlow.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
