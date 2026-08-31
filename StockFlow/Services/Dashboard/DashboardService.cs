using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Dashboard;

namespace StockFlow.Services.Dashboard;

/// <summary>
/// Dashboard metriklerini ve son siparişleri entity yüklemeden hesaplayan salt-okunur Service'tir.
/// </summary>
internal sealed class DashboardService(ApplicationDbContext dbContext) : IDashboardService
{
    private const int RecentOrderCount = 5;

    public async Task<ServiceResult<DashboardViewModel>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var productMetrics = await dbContext.Products
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(products => new ProductMetrics(
                products.Count(),
                products.Count(product =>
                    product.StockQuantity <= product.MinimumStockQuantity)))
            .SingleOrDefaultAsync(cancellationToken);

        var totalCustomerCount = await dbContext.Customers
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var totalSupplierCount = await dbContext.Suppliers
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var orderMetrics = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(orders => new OrderMetrics(
                orders.Count(),
                orders.Sum(order =>
                    order.Type == OrderType.Sale && order.Status == OrderStatus.Confirmed
                        ? order.TotalAmount
                        : 0m)))
            .SingleOrDefaultAsync(cancellationToken);

        var recentOrders = await dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .Take(RecentOrderCount)
            .Select(order => new DashboardRecentOrderViewModel(
                order.Id,
                order.OrderNumber,
                order.Type,
                order.Status,
                order.OrderDate,
                order.TotalAmount,
                order.Type == OrderType.Sale
                    ? order.Customer!.Name
                    : order.Supplier!.CompanyName))
            .ToListAsync(cancellationToken);

        return ServiceResult<DashboardViewModel>.Success(new DashboardViewModel(
            productMetrics?.TotalProductCount ?? 0,
            productMetrics?.LowStockProductCount ?? 0,
            totalCustomerCount,
            totalSupplierCount,
            orderMetrics?.TotalOrderCount ?? 0,
            orderMetrics?.ConfirmedSaleTotalAmount ?? 0m,
            recentOrders));
    }

    private sealed record ProductMetrics(
        int TotalProductCount,
        int LowStockProductCount);

    private sealed record OrderMetrics(
        int TotalOrderCount,
        decimal ConfirmedSaleTotalAmount);
}
