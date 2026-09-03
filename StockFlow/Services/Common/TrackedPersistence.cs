using StockFlow.Data;

namespace StockFlow.Services.Common;

/// <summary>
/// Transaction dışı Service kayıtlarında tracker temizliği ve exception yayılımını tutarlı uygular.
/// </summary>
internal static class TrackedPersistence
{
    internal static async Task SaveChangesAsync(
        ApplicationDbContext dbContext,
        Action<Exception> logUnexpectedFailure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logUnexpectedFailure);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logUnexpectedFailure(exception);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
