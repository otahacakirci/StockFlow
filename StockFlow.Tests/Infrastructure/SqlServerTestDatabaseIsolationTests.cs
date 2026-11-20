using Microsoft.EntityFrameworkCore;
using StockFlow.Entities;

namespace StockFlow.Tests.Infrastructure;

public sealed class SqlServerTestDatabaseIsolationTests : SqlServerDatabaseTestBase
{
    private const int ConcurrentDatabaseCount = 8;

    [Fact]
    public async Task InitializeAsync_WhenDatabasesAreCreatedConcurrently_KeepsDataIsolatedAndRemovesDatabasesOnDispose()
    {
        var testDatabases = Enumerable.Range(0, ConcurrentDatabaseCount)
            .Select(_ => SqlServerTestDatabase.Create())
            .ToArray();
        var databaseNames = testDatabases.Select(database => database.DatabaseName).ToArray();
        Exception? testException = null;

        try
        {
            await Task.WhenAll(testDatabases.Select(database => database.InitializeAsync()));

            for (var index = 0; index < testDatabases.Length; index++)
            {
                await using var dbContext = testDatabases[index].CreateDbContext();
                dbContext.Categories.Add(new Category { Name = $"Izolasyon {index}" });
                await dbContext.SaveChangesAsync();
            }

            for (var index = 0; index < testDatabases.Length; index++)
            {
                await using var dbContext = testDatabases[index].CreateDbContext();
                var categoryNames = await dbContext.Categories
                    .Select(category => category.Name)
                    .ToListAsync();

                Assert.Equal([$"Izolasyon {index}"], categoryNames);
            }
        }
        catch (Exception exception)
        {
            testException = exception;
            throw;
        }
        finally
        {
            var disposeExceptions = new List<Exception>();
            foreach (var testDatabase in testDatabases)
            {
                try
                {
                    await testDatabase.DisposeAsync();
                }
                catch (Exception exception)
                {
                    disposeExceptions.Add(exception);
                }
            }

            if (disposeExceptions.Count > 0)
            {
                if (testException is not null)
                {
                    disposeExceptions.Insert(0, testException);
                }

                throw new AggregateException(
                    "Eşzamanlı test veritabanlarından en az biri güvenli biçimde silinemedi.",
                    disposeExceptions);
            }
        }

        await using var observerContext = CreateDbContext();
        var remainingNames = await observerContext.Database
            .SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.databases")
            .ToListAsync();

        Assert.Empty(remainingNames.Intersect(databaseNames, StringComparer.OrdinalIgnoreCase));
        Assert.Contains(DatabaseName, remainingNames, StringComparer.OrdinalIgnoreCase);
    }
}
