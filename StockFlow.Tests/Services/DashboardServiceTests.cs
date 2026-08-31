using Microsoft.EntityFrameworkCore;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Dashboard;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Dashboard;

namespace StockFlow.Tests.Services;

public sealed class DashboardServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetAsync_WhenDatabaseIsEmpty_ReturnsSafeEmptyDashboard()
    {
        await using var dbContext = CreateDbContext();

        var dashboard = AssertSuccess(await new DashboardService(dbContext).GetAsync());

        Assert.Equal(0, dashboard.TotalProductCount);
        Assert.Equal(0, dashboard.LowStockProductCount);
        Assert.Equal(0, dashboard.TotalCustomerCount);
        Assert.Equal(0, dashboard.TotalSupplierCount);
        Assert.Equal(0, dashboard.TotalOrderCount);
        Assert.Equal(0m, dashboard.ConfirmedSaleTotalAmount);
        Assert.Empty(dashboard.RecentOrders);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetAsync_WithData_ReturnsCountsAndOnlyConfirmedSaleTotal()
    {
        await SeedAsync();

        await using var dbContext = CreateDbContext();
        var dashboard = AssertSuccess(await new DashboardService(dbContext).GetAsync());

        Assert.Equal(3, dashboard.TotalProductCount);
        Assert.Equal(2, dashboard.LowStockProductCount);
        Assert.Equal(2, dashboard.TotalCustomerCount);
        Assert.Equal(2, dashboard.TotalSupplierCount);
        Assert.Equal(6, dashboard.TotalOrderCount);
        Assert.Equal(175m, dashboard.ConfirmedSaleTotalAmount);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetAsync_ReturnsFiveMostRecentOrdersAcrossTypesAndStatuses()
    {
        var seed = await SeedAsync();

        await using var dbContext = CreateDbContext();
        var dashboard = AssertSuccess(await new DashboardService(dbContext).GetAsync());

        var tiedOrderIds = new[] { seed.FirstTiedSaleOrderId, seed.SecondTiedSaleOrderId }
            .OrderByDescending(id => id);
        var expectedOrderIds = tiedOrderIds.Concat(
        [
            seed.PurchaseOrderId,
            seed.CancelledSaleOrderId,
            seed.DraftSaleOrderId
        ]);

        Assert.Equal(expectedOrderIds, dashboard.RecentOrders.Select(order => order.Id));
        Assert.DoesNotContain(
            dashboard.RecentOrders,
            order => order.Id == seed.OldestConfirmedSaleOrderId);

        var purchase = Assert.Single(
            dashboard.RecentOrders,
            order => order.Id == seed.PurchaseOrderId);
        Assert.Equal(OrderType.Purchase, purchase.Type);
        Assert.Equal(OrderStatus.Confirmed, purchase.Status);
        Assert.Equal(seed.SupplierName, purchase.PartyName);
        Assert.Equal(400m, purchase.TotalAmount);

        var tiedSale = Assert.Single(
            dashboard.RecentOrders,
            order => order.Id == seed.SecondTiedSaleOrderId);
        Assert.Equal(seed.SecondTiedSaleOrderNumber, tiedSale.OrderNumber);
        Assert.Equal(OrderType.Sale, tiedSale.Type);
        Assert.Equal(OrderStatus.Confirmed, tiedSale.Status);
        Assert.Equal(seed.CustomerName, tiedSale.PartyName);
        Assert.Equal(UtcDate(5), tiedSale.OrderDate);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetAsync_DoesNotTrackOrMutateProductsOrOrders()
    {
        var seed = await SeedAsync();

        await using (var queryContext = CreateDbContext())
        {
            Assert.True((await new DashboardService(queryContext).GetAsync()).IsSuccess);
            Assert.Empty(queryContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        var stocks = await verificationContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Id)
            .Select(product => product.StockQuantity)
            .ToListAsync();
        Assert.Equal([10, 2, 0], stocks);

        var orders = await verificationContext.Orders
            .AsNoTracking()
            .OrderBy(order => order.Id)
            .Select(order => new OrderState(order.Id, order.Status, order.TotalAmount))
            .ToListAsync();
        Assert.Equal(seed.OrderStates.OrderBy(order => order.Id), orders);
        Assert.Empty(verificationContext.ChangeTracker.Entries());
    }

    private async Task<SeedData> SeedAsync()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "Dashboard Category" };
        var products = new[]
        {
            new Product
            {
                Name = "Normal Stock Product",
                Sku = $"DASH-NORMAL-{Guid.NewGuid():N}"[..32],
                Price = 10m,
                StockQuantity = 10,
                MinimumStockQuantity = 2,
                Category = category
            },
            new Product
            {
                Name = "Threshold Stock Product",
                Sku = $"DASH-EQUAL-{Guid.NewGuid():N}"[..32],
                Price = 2m,
                StockQuantity = 2,
                MinimumStockQuantity = 2,
                Category = category
            },
            new Product
            {
                Name = "Zero Stock Product",
                Sku = $"DASH-ZERO-{Guid.NewGuid():N}"[..32],
                Price = 1m,
                StockQuantity = 0,
                MinimumStockQuantity = 1,
                Category = category
            }
        };
        var customer = new Customer { Name = "Dashboard Customer" };
        var supplier = new Supplier { CompanyName = "Dashboard Supplier" };
        dbContext.AddRange(
            products[0],
            products[1],
            products[2],
            customer,
            new Customer { Name = "Dashboard Count-Only Customer" },
            supplier,
            new Supplier { CompanyName = "Dashboard Count-Only Supplier" });
        await dbContext.SaveChangesAsync();

        var oldestConfirmedSale = CreateOrder(
            OrderType.Sale,
            OrderStatus.Confirmed,
            UtcDate(1),
            25m,
            products[0].Id,
            customer.Id,
            null);
        var draftSale = CreateOrder(
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(2),
            200m,
            products[0].Id,
            customer.Id,
            null);
        var cancelledSale = CreateOrder(
            OrderType.Sale,
            OrderStatus.Cancelled,
            UtcDate(3),
            300m,
            products[0].Id,
            customer.Id,
            null);
        var purchase = CreateOrder(
            OrderType.Purchase,
            OrderStatus.Confirmed,
            UtcDate(4),
            400m,
            products[0].Id,
            null,
            supplier.Id);
        var firstTiedSale = CreateOrder(
            OrderType.Sale,
            OrderStatus.Confirmed,
            UtcDate(5),
            100m,
            products[0].Id,
            customer.Id,
            null);
        var secondTiedSale = CreateOrder(
            OrderType.Sale,
            OrderStatus.Confirmed,
            UtcDate(5),
            50m,
            products[0].Id,
            customer.Id,
            null);

        dbContext.AddRange(
            oldestConfirmedSale,
            draftSale,
            cancelledSale,
            purchase,
            firstTiedSale,
            secondTiedSale);
        await dbContext.SaveChangesAsync();

        return new SeedData(
            customer.Name,
            supplier.CompanyName,
            oldestConfirmedSale.Id,
            draftSale.Id,
            cancelledSale.Id,
            purchase.Id,
            firstTiedSale.Id,
            secondTiedSale.Id,
            secondTiedSale.OrderNumber,
            [
                new(oldestConfirmedSale.Id, oldestConfirmedSale.Status, oldestConfirmedSale.TotalAmount),
                new(draftSale.Id, draftSale.Status, draftSale.TotalAmount),
                new(cancelledSale.Id, cancelledSale.Status, cancelledSale.TotalAmount),
                new(purchase.Id, purchase.Status, purchase.TotalAmount),
                new(firstTiedSale.Id, firstTiedSale.Status, firstTiedSale.TotalAmount),
                new(secondTiedSale.Id, secondTiedSale.Status, secondTiedSale.TotalAmount)
            ]);
    }

    private static Order CreateOrder(
        OrderType type,
        OrderStatus status,
        DateTime orderDate,
        decimal totalAmount,
        int productId,
        int? customerId,
        int? supplierId)
    {
        return new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = type,
            Status = status,
            OrderDate = orderDate,
            TotalAmount = totalAmount,
            CustomerId = customerId,
            SupplierId = supplierId,
            Items =
            [
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = totalAmount
                }
            ]
        };
    }

    private static DateTime UtcDate(int day)
    {
        return new DateTime(2026, 8, day, 12, 0, 0, DateTimeKind.Utc);
    }

    private static T AssertSuccess<T>(ServiceResult<T> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Value);
    }

    private sealed record SeedData(
        string CustomerName,
        string SupplierName,
        int OldestConfirmedSaleOrderId,
        int DraftSaleOrderId,
        int CancelledSaleOrderId,
        int PurchaseOrderId,
        int FirstTiedSaleOrderId,
        int SecondTiedSaleOrderId,
        string SecondTiedSaleOrderNumber,
        IReadOnlyList<OrderState> OrderStates);

    private sealed record OrderState(
        int Id,
        OrderStatus Status,
        decimal TotalAmount);
}
