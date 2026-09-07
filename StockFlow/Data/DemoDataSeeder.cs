using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using StockFlow.Options;
using StockFlow.Security;

namespace StockFlow.Data;

internal sealed class DemoDataSeeder(
    ApplicationDbContext dbContext,
    IHostEnvironment hostEnvironment,
    IOptions<DemoDataOptions> options,
    IOptions<IdentitySeedOptions> identitySeedOptions,
    ILogger<DemoDataSeeder> logger)
{
    private readonly DemoDataOptions options = options.Value;
    private readonly IdentitySeedOptions identitySeedOptions = identitySeedOptions.Value;

    internal async Task<DemoDataSeedOutcome> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            logger.LogDebug("Demo veri başlangıç yüklemesi yapılandırma ile kapalıdır.");
            return DemoDataSeedOutcome.Disabled;
        }

        if (!hostEnvironment.IsDevelopment())
        {
            logger.LogInformation(
                "Demo veri başlangıç yüklemesi yalnız Development ortamında çalıştığı için atlandı. Ortam: {EnvironmentName}.",
                hostEnvironment.EnvironmentName);
            return DemoDataSeedOutcome.NonDevelopmentEnvironment;
        }

        try
        {
            return await SeedCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Demo veri başlangıç yüklemesi başarısız oldu; transaction geri alındı.");
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<DemoDataSeedOutcome> SeedCoreAsync(
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (await HasAnyDomainDataAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogInformation(
                    "Demo veri başlangıç yüklemesi mevcut domain verisi bulunduğu için atlandı; hiçbir mevcut kayıt değiştirilmedi.");
                return DemoDataSeedOutcome.ExistingDomainData;
            }

            var creatorUserIds = await LoadCreatorUserIdsAsync(cancellationToken);
            var dataSet = DemoDataCatalog.Create(
                creatorUserIds.AdminUserId,
                creatorUserIds.EmployeeUserId);

            dbContext.Categories.AddRange(dataSet.Categories);
            dbContext.Products.AddRange(dataSet.Products);
            dbContext.Customers.AddRange(dataSet.Customers);
            dbContext.Suppliers.AddRange(dataSet.Suppliers);
            dbContext.Orders.AddRange(dataSet.Orders);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Demo veri başlangıç yüklemesi tamamlandı: {CategoryCount} kategori, {ProductCount} ürün, {CustomerCount} müşteri, {SupplierCount} tedarikçi, {OrderCount} sipariş ve {StockMovementCount} stok hareketi.",
                dataSet.Categories.Count,
                dataSet.Products.Count,
                dataSet.Customers.Count,
                dataSet.Suppliers.Count,
                dataSet.Orders.Count,
                dataSet.Orders.Sum(order => order.StockMovements.Count));

            return DemoDataSeedOutcome.Seeded;
        }
        catch
        {
            await RollbackWithoutMaskingAsync(transaction);
            throw;
        }
    }

    private async Task<bool> HasAnyDomainDataAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Categories.AnyAsync(cancellationToken)
            || await dbContext.Products.AnyAsync(cancellationToken)
            || await dbContext.Customers.AnyAsync(cancellationToken)
            || await dbContext.Suppliers.AnyAsync(cancellationToken)
            || await dbContext.Orders.AnyAsync(cancellationToken)
            || await dbContext.OrderItems.AnyAsync(cancellationToken)
            || await dbContext.StockMovements.AnyAsync(cancellationToken);
    }

    private async Task<DemoCreatorUserIds> LoadCreatorUserIdsAsync(
        CancellationToken cancellationToken)
    {
        var normalizedAdminEmail = identitySeedOptions.Admin.Email.ToUpperInvariant();
        var normalizedEmployeeEmail = identitySeedOptions.Employee.Email.ToUpperInvariant();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedAdminEmail
                || user.NormalizedEmail == normalizedEmployeeEmail)
            .Select(user => new DemoIdentityUser(user.Id, user.NormalizedEmail!))
            .ToListAsync(cancellationToken);

        var adminUserId = users
            .SingleOrDefault(user => user.NormalizedEmail == normalizedAdminEmail)
            ?.UserId;
        var employeeUserId = users
            .SingleOrDefault(user => user.NormalizedEmail == normalizedEmployeeEmail)
            ?.UserId;

        if (string.IsNullOrWhiteSpace(adminUserId)
            || string.IsNullOrWhiteSpace(employeeUserId))
        {
            throw new InvalidOperationException(
                "Demo veri yüklemesi için Identity başlangıç kullanıcıları ve rol üyelikleri bulunamadı.");
        }

        var requiredMembershipCount = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where (userRole.UserId == adminUserId && role.Name == AppRoles.Admin)
                || (userRole.UserId == employeeUserId && role.Name == AppRoles.Employee)
            select userRole)
            .CountAsync(cancellationToken);
        if (requiredMembershipCount != 2)
        {
            throw new InvalidOperationException(
                "Demo veri yüklemesi için Identity başlangıç kullanıcıları ve rol üyelikleri bulunamadı.");
        }

        return new DemoCreatorUserIds(adminUserId, employeeUserId);
    }

    private async Task RollbackWithoutMaskingAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            logger.LogError(
                rollbackException,
                "Demo veri başlangıç transaction geri alma işlemi başarısız oldu.");
        }
    }

    private sealed record DemoIdentityUser(string UserId, string NormalizedEmail);

    private sealed record DemoCreatorUserIds(string AdminUserId, string EmployeeUserId);
}

internal enum DemoDataSeedOutcome
{
    Disabled,
    NonDevelopmentEnvironment,
    ExistingDomainData,
    Seeded
}
