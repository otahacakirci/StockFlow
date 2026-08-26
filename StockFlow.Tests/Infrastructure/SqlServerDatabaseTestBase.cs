using Microsoft.Extensions.DependencyInjection;
using StockFlow.Data;

namespace StockFlow.Tests.Infrastructure;

public abstract class SqlServerDatabaseTestBase : IAsyncLifetime
{
    private readonly SqlServerTestDatabase _testDatabase = SqlServerTestDatabase.Create();

    protected string DatabaseName => _testDatabase.DatabaseName;

    protected void AddTestDbContext(IServiceCollection services)
    {
        _testDatabase.AddDbContext(services);
    }

    protected ApplicationDbContext CreateDbContext()
    {
        return _testDatabase.CreateDbContext();
    }

    public Task InitializeAsync()
    {
        return _testDatabase.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _testDatabase.DisposeAsync();
    }
}
