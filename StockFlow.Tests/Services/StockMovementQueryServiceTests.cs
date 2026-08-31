using Microsoft.EntityFrameworkCore;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.StockMovements;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Tests.Services;

public sealed class StockMovementQueryServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesRelationshipTypeAndDateFiltersWithSafeProjection()
    {
        var seed = await SeedAsync();
        var expectedMovementId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            4,
            "Purchase stock receipt.",
            UtcDate(2, 10));
        await SeedMovementAsync(
            seed.SecondProductId,
            seed.SaleOrderId,
            StockMovementType.StockOut,
            2,
            "Sale stock issue.",
            UtcDate(2, 12));

        await using var dbContext = CreateDbContext();
        var result = AssertSuccess(await new StockMovementQueryService(dbContext).GetListAsync(
            new StockMovementListQueryModel
            {
                ProductId = seed.FirstProductId,
                OrderId = seed.PurchaseOrderId,
                Type = StockMovementType.StockIn,
                StartDate = new DateOnly(2026, 8, 2),
                EndDate = new DateOnly(2026, 8, 2)
            }));

        Assert.Equal(seed.FirstProductId, result.ProductId);
        Assert.Equal(seed.PurchaseOrderId, result.OrderId);
        Assert.Equal(StockMovementType.StockIn, result.Type);
        Assert.Equal(new DateOnly(2026, 8, 2), result.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 2), result.EndDate);
        var movement = Assert.Single(result.Items);
        Assert.Equal(expectedMovementId, movement.Id);
        Assert.Equal(seed.FirstProductName, movement.ProductName);
        Assert.Equal(seed.FirstProductSku, movement.ProductSku);
        Assert.Equal(seed.PurchaseOrderNumber, movement.OrderNumber);
        Assert.Equal(StockMovementType.StockIn, movement.Type);
        Assert.Equal(4, movement.Quantity);
        Assert.Equal("Purchase stock receipt.", movement.Description);
        Assert.Equal(UtcDate(2, 10), movement.MovementDateUtc.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, movement.MovementDateUtc.Offset);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_IncludesBothUtcDateBoundaries()
    {
        var seed = await SeedAsync();
        var start = new DateOnly(2026, 8, 2).ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Utc);
        var end = new DateOnly(2026, 8, 2).ToDateTime(
            TimeOnly.MaxValue,
            DateTimeKind.Utc);
        var startId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            1,
            "Start boundary.",
            start);
        var endId = await SeedMovementAsync(
            seed.SecondProductId,
            seed.SaleOrderId,
            StockMovementType.StockOut,
            1,
            "End boundary.",
            end);
        await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            1,
            "Outside range.",
            UtcDate(3, 0));

        await using var dbContext = CreateDbContext();
        var result = AssertSuccess(await new StockMovementQueryService(dbContext).GetListAsync(
            new StockMovementListQueryModel
            {
                StartDate = new DateOnly(2026, 8, 2),
                EndDate = new DateOnly(2026, 8, 2)
            }));

        Assert.Equal([endId, startId], result.Items.Select(movement => movement.Id));
        Assert.All(result.Items, movement =>
            Assert.Equal(TimeSpan.Zero, movement.MovementDateUtc.Offset));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_AppliesStableDateSortAndServerPagination()
    {
        var seed = await SeedAsync();
        var oldestId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            1,
            "Oldest movement.",
            UtcDate(1, 9));
        var firstTiedId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            1,
            "First tied movement.",
            UtcDate(3, 9));
        var secondTiedId = await SeedMovementAsync(
            seed.SecondProductId,
            seed.SaleOrderId,
            StockMovementType.StockOut,
            1,
            "Second tied movement.",
            UtcDate(3, 9));

        await using var dbContext = CreateDbContext();
        var service = new StockMovementQueryService(dbContext);
        var descending = AssertSuccess(await service.GetListAsync(
            new StockMovementListQueryModel
            {
                Page = 1,
                PageSize = 2
            }));

        Assert.Equal(StockMovementSortOrder.DateDescending, descending.SortOrder);
        Assert.Equal(3, descending.TotalCount);
        Assert.Equal(2, descending.TotalPages);
        Assert.Equal([secondTiedId, firstTiedId], descending.Items.Select(item => item.Id));

        var ascendingSecondPage = AssertSuccess(await service.GetListAsync(
            new StockMovementListQueryModel
            {
                SortOrder = StockMovementSortOrder.DateAscending,
                Page = 2,
                PageSize = 2
            }));

        Assert.Equal(StockMovementSortOrder.DateAscending, ascendingSecondPage.SortOrder);
        Assert.Equal(2, ascendingSecondPage.Page);
        Assert.Equal(secondTiedId, Assert.Single(ascendingSecondPage.Items).Id);

        var ascendingFirst = AssertSuccess(await service.GetListAsync(
            new StockMovementListQueryModel
            {
                SortOrder = StockMovementSortOrder.DateAscending,
                PageSize = 1
            }));
        Assert.Equal(oldestId, Assert.Single(ascendingFirst.Items).Id);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidValuesAndHandlesEmptyResults()
    {
        var seed = await SeedAsync();
        var olderId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            1,
            "Older movement.",
            UtcDate(1, 9));
        var newerId = await SeedMovementAsync(
            seed.SecondProductId,
            seed.SaleOrderId,
            StockMovementType.StockOut,
            1,
            "Newer movement.",
            UtcDate(2, 9));

        await using var dbContext = CreateDbContext();
        var service = new StockMovementQueryService(dbContext);
        var normalized = AssertSuccess(await service.GetListAsync(
            new StockMovementListQueryModel
            {
                ProductId = -1,
                OrderId = 0,
                Type = (StockMovementType)int.MaxValue,
                SortOrder = (StockMovementSortOrder)int.MaxValue,
                Page = int.MaxValue,
                PageSize = int.MaxValue
            }));

        Assert.Null(normalized.ProductId);
        Assert.Null(normalized.OrderId);
        Assert.Null(normalized.Type);
        Assert.Equal(StockMovementSortOrder.DateDescending, normalized.SortOrder);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Equal([newerId, olderId], normalized.Items.Select(item => item.Id));

        var empty = AssertSuccess(await service.GetListAsync(new StockMovementListQueryModel
        {
            ProductId = int.MaxValue,
            Page = -1,
            PageSize = 0
        }));

        Assert.Equal(int.MaxValue, empty.ProductId);
        Assert.Equal(1, empty.Page);
        Assert.Equal(20, empty.PageSize);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_WhenDateRangeIsReversed_ReturnsValidation()
    {
        await using var dbContext = CreateDbContext();
        var result = await new StockMovementQueryService(dbContext).GetListAsync(
            new StockMovementListQueryModel
            {
                StartDate = new DateOnly(2026, 8, 3),
                EndDate = new DateOnly(2026, 8, 2)
            });

        AssertFailure(
            result,
            ServiceErrorCategory.Validation,
            StockMovementQueryServiceErrorCodes.InvalidDateRange);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProjectionAndNotFound()
    {
        var seed = await SeedAsync();
        var movementId = await SeedMovementAsync(
            seed.SecondProductId,
            seed.SaleOrderId,
            StockMovementType.StockOut,
            3,
            "Detailed movement.",
            UtcDate(4, 14));

        await using var dbContext = CreateDbContext();
        var service = new StockMovementQueryService(dbContext);
        var movement = AssertSuccess(await service.GetByIdAsync(movementId));

        Assert.Equal(movementId, movement.Id);
        Assert.Equal(seed.SecondProductId, movement.ProductId);
        Assert.Equal(seed.SecondProductName, movement.ProductName);
        Assert.Equal(seed.SecondProductSku, movement.ProductSku);
        Assert.Equal(seed.SaleOrderId, movement.OrderId);
        Assert.Equal(seed.SaleOrderNumber, movement.OrderNumber);
        Assert.Equal(StockMovementType.StockOut, movement.Type);
        Assert.Equal(3, movement.Quantity);
        Assert.Equal("Detailed movement.", movement.Description);
        Assert.Equal(UtcDate(4, 14), movement.MovementDateUtc.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, movement.MovementDateUtc.Offset);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            StockMovementQueryServiceErrorCodes.StockMovementNotFound);
    }

    [Fact]
    public async Task QueryOperations_DoNotChangeProductsOrdersOrMovements()
    {
        var seed = await SeedAsync();
        var movementId = await SeedMovementAsync(
            seed.FirstProductId,
            seed.PurchaseOrderId,
            StockMovementType.StockIn,
            2,
            "Immutable audit movement.",
            UtcDate(5, 11));

        await using (var queryContext = CreateDbContext())
        {
            var service = new StockMovementQueryService(queryContext);
            Assert.True((await service.GetListAsync()).IsSuccess);
            Assert.True((await service.GetByIdAsync(movementId)).IsSuccess);
            Assert.Empty(queryContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            10,
            await verificationContext.Products
                .Where(product => product.Id == seed.FirstProductId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
        Assert.Equal(
            OrderStatus.Confirmed,
            await verificationContext.Orders
                .Where(order => order.Id == seed.PurchaseOrderId)
                .Select(order => order.Status)
                .SingleAsync());
        var movement = await verificationContext.StockMovements
            .SingleAsync(candidate => candidate.Id == movementId);
        Assert.Equal(2, movement.Quantity);
        Assert.Equal("Immutable audit movement.", movement.Description);
        Assert.Equal(1, await verificationContext.StockMovements.CountAsync());
    }

    private async Task<SeedData> SeedAsync()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "Stock Movement Query Category" };
        var customer = new Customer { Name = "Stock Movement Query Customer" };
        var supplier = new Supplier { CompanyName = "Stock Movement Query Supplier" };
        var firstProduct = new Product
        {
            Name = "First Movement Product",
            Sku = $"MOVEMENT-FIRST-{Guid.NewGuid():N}"[..32],
            Price = 12.50m,
            StockQuantity = 10,
            MinimumStockQuantity = 2,
            Category = category
        };
        var secondProduct = new Product
        {
            Name = "Second Movement Product",
            Sku = $"MOVEMENT-SECOND-{Guid.NewGuid():N}"[..32],
            Price = 7.25m,
            StockQuantity = 5,
            MinimumStockQuantity = 1,
            Category = category
        };
        var purchaseOrder = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = OrderType.Purchase,
            Status = OrderStatus.Confirmed,
            OrderDate = UtcDate(1, 8),
            TotalAmount = 12.50m,
            Supplier = supplier,
            Items =
            [
                new OrderItem
                {
                    Product = firstProduct,
                    Quantity = 1,
                    UnitPrice = 12.50m
                }
            ]
        };
        var saleOrder = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = OrderType.Sale,
            Status = OrderStatus.Confirmed,
            OrderDate = UtcDate(1, 9),
            TotalAmount = 7.25m,
            Customer = customer,
            Items =
            [
                new OrderItem
                {
                    Product = secondProduct,
                    Quantity = 1,
                    UnitPrice = 7.25m
                }
            ]
        };

        dbContext.AddRange(purchaseOrder, saleOrder);
        await dbContext.SaveChangesAsync();

        return new SeedData(
            firstProduct.Id,
            firstProduct.Name,
            firstProduct.Sku,
            secondProduct.Id,
            secondProduct.Name,
            secondProduct.Sku,
            purchaseOrder.Id,
            purchaseOrder.OrderNumber,
            saleOrder.Id,
            saleOrder.OrderNumber);
    }

    private async Task<int> SeedMovementAsync(
        int productId,
        int orderId,
        StockMovementType type,
        int quantity,
        string description,
        DateTime movementDate)
    {
        await using var dbContext = CreateDbContext();
        var movement = new StockMovement
        {
            ProductId = productId,
            OrderId = orderId,
            Type = type,
            Quantity = quantity,
            Description = description,
            MovementDate = movementDate
        };

        dbContext.StockMovements.Add(movement);
        await dbContext.SaveChangesAsync();
        return movement.Id;
    }

    private static DateTime UtcDate(int day, int hour)
    {
        return new DateTime(2026, 8, day, hour, 0, 0, DateTimeKind.Utc);
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
        int FirstProductId,
        string FirstProductName,
        string FirstProductSku,
        int SecondProductId,
        string SecondProductName,
        string SecondProductSku,
        int PurchaseOrderId,
        string PurchaseOrderNumber,
        int SaleOrderId,
        string SaleOrderNumber);
}
