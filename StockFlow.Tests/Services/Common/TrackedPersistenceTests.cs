using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;

namespace StockFlow.Tests.Services.Common;

public sealed class TrackedPersistenceTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenPersistenceFails_LogsClearsTrackerAndRethrowsSameException()
    {
        var failure = new TestPersistenceException();
        await using var dbContext = CreateThrowingContext(failure);
        dbContext.Customers.Add(new Customer { Name = "Tracked customer" });
        Exception? loggedException = null;

        var thrown = await Assert.ThrowsAsync<TestPersistenceException>(() =>
            TrackedPersistence.SaveChangesAsync(
                dbContext,
                exception => loggedException = exception,
                CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Same(failure, loggedException);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenCancelled_ClearsTrackerWithoutLoggingAndRethrows()
    {
        var cancellation = new OperationCanceledException();
        await using var dbContext = CreateThrowingContext(cancellation);
        dbContext.Customers.Add(new Customer { Name = "Tracked customer" });
        var logCalled = false;

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TrackedPersistence.SaveChangesAsync(
                dbContext,
                _ => logCalled = true,
                CancellationToken.None));

        Assert.Same(cancellation, thrown);
        Assert.False(logCalled);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    private static ApplicationDbContext CreateThrowingContext(Exception exception)
    {
        return new ThrowingApplicationDbContext(exception);
    }

    private sealed class ThrowingApplicationDbContext(Exception exception)
        : ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused")
            .Options)
    {
        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<int>(exception);
        }
    }

    private sealed class TestPersistenceException : Exception;
}
