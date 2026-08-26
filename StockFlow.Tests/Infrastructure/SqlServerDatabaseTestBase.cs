using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    protected ApplicationDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        return _testDatabase.CreateDbContext(interceptors);
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
