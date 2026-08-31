using Microsoft.EntityFrameworkCore;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Orders;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Tests.Services;

public sealed class OrderQueryServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesTypeAndStatusFiltersWithPartyAndItemProjections()
    {
        var seed = await SeedAsync();
        await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(1),
            (seed.FirstProductId, 1, 12.50m));
        var confirmedSaleId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Confirmed,
            UtcDate(3),
            (seed.FirstProductId, 2, 12.50m),
            (seed.SecondProductId, 1, 7.25m));
        var purchaseId = await SeedOrderAsync(
            seed,
            OrderType.Purchase,
            OrderStatus.Draft,
            UtcDate(2),
            (seed.SecondProductId, 3, 7.25m));

        await using var dbContext = CreateDbContext();
        var service = new OrderQueryService(dbContext);

        var saleResult = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            Type = OrderType.Sale,
            Status = OrderStatus.Confirmed
        }));

        Assert.Equal(OrderType.Sale, saleResult.Type);
        Assert.Equal(OrderStatus.Confirmed, saleResult.Status);
        var sale = Assert.Single(saleResult.Items);
        Assert.Equal(confirmedSaleId, sale.Id);
        Assert.Equal(seed.CustomerName, sale.PartyName);
        Assert.Equal(2, sale.ItemCount);
        Assert.Equal(32, sale.OrderNumber.Length);
        Assert.Equal(32.25m, sale.TotalAmount);

        var purchaseResult = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            Type = OrderType.Purchase,
            Status = OrderStatus.Draft
        }));
        var purchase = Assert.Single(purchaseResult.Items);
        Assert.Equal(purchaseId, purchase.Id);
        Assert.Equal(seed.SupplierCompanyName, purchase.PartyName);
        Assert.Equal(1, purchase.ItemCount);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_AppliesStableDateSortAndServerPagination()
    {
        var seed = await SeedAsync();
        var oldestId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(1),
            (seed.FirstProductId, 1, 12.50m));
        var firstTiedId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(3),
            (seed.FirstProductId, 1, 12.50m));
        var secondTiedId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(3),
            (seed.SecondProductId, 1, 7.25m));

        await using var dbContext = CreateDbContext();
        var service = new OrderQueryService(dbContext);

        var descending = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            Page = 1,
            PageSize = 2
        }));

        Assert.Equal(OrderSortOrder.DateDescending, descending.SortOrder);
        Assert.Equal(3, descending.TotalCount);
        Assert.Equal(2, descending.TotalPages);
        Assert.Equal([secondTiedId, firstTiedId], descending.Items.Select(order => order.Id));

        var ascendingSecondPage = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            SortOrder = OrderSortOrder.DateAscending,
            Page = 2,
            PageSize = 2
        }));

        Assert.Equal(OrderSortOrder.DateAscending, ascendingSecondPage.SortOrder);
        Assert.Equal(2, ascendingSecondPage.Page);
        Assert.Equal(secondTiedId, Assert.Single(ascendingSecondPage.Items).Id);
        Assert.Equal(oldestId, (await service.GetListAsync(new OrderListQueryModel
        {
            SortOrder = OrderSortOrder.DateAscending,
            PageSize = 1
        })).Value!.Items.Single().Id);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidValuesAndHandlesEmptyResults()
    {
        var seed = await SeedAsync();
        var olderId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(1),
            (seed.FirstProductId, 1, 12.50m));
        var newerId = await SeedOrderAsync(
            seed,
            OrderType.Purchase,
            OrderStatus.Confirmed,
            UtcDate(2),
            (seed.SecondProductId, 1, 7.25m));

        await using var dbContext = CreateDbContext();
        var service = new OrderQueryService(dbContext);

        var normalized = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            Type = (OrderType)int.MaxValue,
            Status = (OrderStatus)int.MaxValue,
            SortOrder = (OrderSortOrder)int.MaxValue,
            Page = int.MaxValue,
            PageSize = int.MaxValue
        }));

        Assert.Null(normalized.Type);
        Assert.Null(normalized.Status);
        Assert.Equal(OrderSortOrder.DateDescending, normalized.SortOrder);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Equal([newerId, olderId], normalized.Items.Select(order => order.Id));

        var empty = AssertSuccess(await service.GetListAsync(new OrderListQueryModel
        {
            Type = OrderType.Purchase,
            Status = OrderStatus.Cancelled,
            Page = -1,
            PageSize = 0
        }));

        Assert.Equal(OrderType.Purchase, empty.Type);
        Assert.Equal(OrderStatus.Cancelled, empty.Status);
        Assert.Equal(1, empty.Page);
        Assert.Equal(20, empty.PageSize);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSaleAndPurchaseDetailsWithProductSnapshotsAndNotFound()
    {
        var seed = await SeedAsync();
        var saleId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(1),
            (seed.FirstProductId, 2, 12.50m),
            (seed.SecondProductId, 3, 7.25m));
        var purchaseId = await SeedOrderAsync(
            seed,
            OrderType.Purchase,
            OrderStatus.Confirmed,
            UtcDate(2),
            (seed.SecondProductId, 4, 7.25m));

        await using var dbContext = CreateDbContext();
        var service = new OrderQueryService(dbContext);

        var sale = AssertSuccess(await service.GetByIdAsync(saleId));
        Assert.Equal(seed.CustomerId, sale.CustomerId);
        Assert.Equal(seed.CustomerName, sale.CustomerName);
        Assert.Null(sale.SupplierId);
        Assert.Null(sale.SupplierCompanyName);
        Assert.Equal(46.75m, sale.TotalAmount);
        Assert.Equal(2, sale.Items.Count);
        var firstItem = sale.Items.Single(item => item.ProductId == seed.FirstProductId);
        Assert.Equal(seed.FirstProductName, firstItem.ProductName);
        Assert.Equal(seed.FirstProductSku, firstItem.ProductSku);
        Assert.Equal(2, firstItem.Quantity);
        Assert.Equal(12.50m, firstItem.UnitPrice);
        Assert.Equal(25.00m, firstItem.LineTotal);

        var purchase = AssertSuccess(await service.GetByIdAsync(purchaseId));
        Assert.Null(purchase.CustomerId);
        Assert.Null(purchase.CustomerName);
        Assert.Equal(seed.SupplierId, purchase.SupplierId);
        Assert.Equal(seed.SupplierCompanyName, purchase.SupplierCompanyName);
        Assert.Equal(29.00m, Assert.Single(purchase.Items).LineTotal);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.OrderNotFound);
    }

    [Fact]
    public async Task GetDraftForEditAsync_ReturnsCurrentPartyAndProductDataAndNotFound()
    {
        var seed = await SeedAsync();
        var orderId = await SeedOrderAsync(
            seed,
            OrderType.Purchase,
            OrderStatus.Draft,
            UtcDate(2),
            (seed.FirstProductId, 3, 11.00m),
            (seed.SecondProductId, 2, 7.25m));

        await using var dbContext = CreateDbContext();
        var service = new OrderQueryService(dbContext);
        var editModel = AssertSuccess(await service.GetDraftForEditAsync(orderId));

        Assert.Equal(orderId, editModel.Id);
        Assert.Equal(UtcDate(2), editModel.OrderDate);
        Assert.Equal(OrderType.Purchase, editModel.Type);
        Assert.Null(editModel.CustomerId);
        Assert.Equal(seed.SupplierId, editModel.SupplierId);
        Assert.Equal(47.50m, editModel.TotalAmount);
        Assert.Equal(2, editModel.Items.Count);
        var firstItem = editModel.Items.Single(item => item.ProductId == seed.FirstProductId);
        Assert.Equal(seed.FirstProductName, firstItem.ProductName);
        Assert.Equal(seed.FirstProductSku, firstItem.ProductSku);
        Assert.Equal(3, firstItem.Quantity);
        Assert.Equal(11.00m, firstItem.UnitPrice);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetDraftForEditAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.OrderNotFound);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task GetDraftForEditAsync_WhenOrderIsTerminal_ReturnsBusinessRule(
        OrderStatus status)
    {
        var seed = await SeedAsync();
        var orderId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            status,
            UtcDate(1),
            (seed.FirstProductId, 1, 12.50m));

        await using var dbContext = CreateDbContext();
        var result = await new OrderQueryService(dbContext).GetDraftForEditAsync(orderId);

        AssertFailure(
            result,
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task QueryOperations_DoNotChangeOrdersStockOrMovements()
    {
        var seed = await SeedAsync();
        var draftId = await SeedOrderAsync(
            seed,
            OrderType.Sale,
            OrderStatus.Draft,
            UtcDate(1),
            (seed.FirstProductId, 1, 12.50m));
        var confirmedId = await SeedOrderAsync(
            seed,
            OrderType.Purchase,
            OrderStatus.Confirmed,
            UtcDate(2),
            (seed.SecondProductId, 2, 7.25m));
        await SeedMovementAsync(confirmedId, seed.SecondProductId);

        await using (var queryContext = CreateDbContext())
        {
            var service = new OrderQueryService(queryContext);
            Assert.True((await service.GetListAsync()).IsSuccess);
            Assert.True((await service.GetByIdAsync(confirmedId)).IsSuccess);
            Assert.True((await service.GetDraftForEditAsync(draftId)).IsSuccess);
            Assert.Empty(queryContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            OrderStatus.Draft,
            await verificationContext.Orders
                .Where(order => order.Id == draftId)
                .Select(order => order.Status)
                .SingleAsync());
        Assert.Equal(
            OrderStatus.Confirmed,
            await verificationContext.Orders
                .Where(order => order.Id == confirmedId)
                .Select(order => order.Status)
                .SingleAsync());
        Assert.Equal(
            10,
            await verificationContext.Products
                .Where(product => product.Id == seed.FirstProductId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
        Assert.Equal(
            5,
            await verificationContext.Products
                .Where(product => product.Id == seed.SecondProductId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
        Assert.Equal(1, await verificationContext.StockMovements.CountAsync());
    }

    private async Task<SeedData> SeedAsync()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "Order Query Category" };
        var customer = new Customer { Name = "Order Query Customer" };
        var supplier = new Supplier { CompanyName = "Order Query Supplier" };
        var firstProduct = new Product
        {
            Name = "First Query Product",
            Sku = $"QUERY-FIRST-{Guid.NewGuid():N}"[..32],
            Price = 12.50m,
            StockQuantity = 10,
            MinimumStockQuantity = 2,
            Category = category
        };
        var secondProduct = new Product
        {
            Name = "Second Query Product",
            Sku = $"QUERY-SECOND-{Guid.NewGuid():N}"[..32],
            Price = 7.25m,
            StockQuantity = 5,
            MinimumStockQuantity = 1,
            Category = category
        };

        dbContext.AddRange(customer, supplier, firstProduct, secondProduct);
        await dbContext.SaveChangesAsync();

        return new SeedData(
            customer.Id,
            customer.Name,
            supplier.Id,
            supplier.CompanyName,
            firstProduct.Id,
            firstProduct.Name,
            firstProduct.Sku,
            secondProduct.Id);
    }

    private async Task<int> SeedOrderAsync(
        SeedData seed,
        OrderType type,
        OrderStatus status,
        DateTime orderDate,
        params (int ProductId, int Quantity, decimal UnitPrice)[] items)
    {
        await using var dbContext = CreateDbContext();
        var order = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = type,
            Status = status,
            OrderDate = orderDate,
            TotalAmount = items.Sum(item => item.Quantity * item.UnitPrice),
            CustomerId = type == OrderType.Sale ? seed.CustomerId : null,
            SupplierId = type == OrderType.Purchase ? seed.SupplierId : null,
            Items = items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task SeedMovementAsync(int orderId, int productId)
    {
        await using var dbContext = CreateDbContext();
        dbContext.StockMovements.Add(new StockMovement
        {
            OrderId = orderId,
            ProductId = productId,
            Type = StockMovementType.StockIn,
            Quantity = 2,
            Description = "Existing query test movement.",
            MovementDate = UtcDate(2)
        });
        await dbContext.SaveChangesAsync();
    }

    private static DateTime UtcDate(int day)
    {
        return new DateTime(2026, 8, day, 9, 30, 0, DateTimeKind.Utc);
    }

    private static T AssertSuccess<T>(ServiceResult<T> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Value);
    }

    private static void AssertFailure<T>(
        ServiceResult<T> result,
        ServiceErrorCategory category,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(category, result.Error.Category);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record SeedData(
        int CustomerId,
        string CustomerName,
        int SupplierId,
        string SupplierCompanyName,
        int FirstProductId,
        string FirstProductName,
        string FirstProductSku,
        int SecondProductId);
}
