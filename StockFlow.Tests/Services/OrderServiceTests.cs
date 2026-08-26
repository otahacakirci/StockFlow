using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Orders;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Tests.Services;

public sealed class OrderServiceTests : SqlServerDatabaseTestBase
{
    private const string CreatorUserId = "order-service-test-user";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 26, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateDraftAsync_CreatesSaleAndPurchaseWithServerPricesWithoutChangingStock()
    {
        var seed = await SeedAsync();

        await using (var dbContext = CreateDbContext())
        {
            var service = CreateService(dbContext);
            var saleResult = await service.CreateDraftAsync(
                SaleInput(seed, (seed.FirstProductId, 2)),
                CreatorUserId);
            var purchaseResult = await service.CreateDraftAsync(
                PurchaseInput(seed, (seed.SecondProductId, 3)),
                CreatorUserId);

            var sale = AssertSuccess(saleResult);
            var purchase = AssertSuccess(purchaseResult);

            Assert.Equal(OrderStatus.Draft, sale.Status);
            Assert.Equal(25.00m, sale.TotalAmount);
            Assert.Equal(OrderStatus.Draft, purchase.Status);
            Assert.Equal(21.75m, purchase.TotalAmount);
            Assert.Matches("^[0-9a-f]{32}$", sale.OrderNumber);
            Assert.Matches("^[0-9a-f]{32}$", purchase.OrderNumber);
            Assert.NotEqual(sale.OrderNumber, purchase.OrderNumber);
        }

        await using var verificationContext = CreateDbContext();
        var orders = await verificationContext.Orders
            .Include(order => order.Items)
            .OrderBy(order => order.Type)
            .ToListAsync();
        var products = await verificationContext.Products
            .OrderBy(product => product.Id)
            .ToListAsync();

        Assert.Equal(2, orders.Count);
        var saleOrder = Assert.Single(orders, order => order.Type == OrderType.Sale);
        Assert.Equal(seed.CustomerId, saleOrder.CustomerId);
        Assert.Null(saleOrder.SupplierId);
        Assert.Equal(CreatorUserId, saleOrder.CreatedByUserId);
        Assert.Equal(FixedUtcNow.UtcDateTime, saleOrder.OrderDate);
        Assert.Equal(12.50m, Assert.Single(saleOrder.Items).UnitPrice);

        var purchaseOrder = Assert.Single(orders, order => order.Type == OrderType.Purchase);
        Assert.Equal(seed.SupplierId, purchaseOrder.SupplierId);
        Assert.Null(purchaseOrder.CustomerId);
        Assert.Equal(7.25m, Assert.Single(purchaseOrder.Items).UnitPrice);
        Assert.Equal(10, products.Single(product => product.Id == seed.FirstProductId).StockQuantity);
        Assert.Equal(5, products.Single(product => product.Id == seed.SecondProductId).StockQuantity);
        Assert.Empty(await verificationContext.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task UpdateDraftAsync_PreservesExistingSnapshotAndUsesCurrentPriceForNewItem()
    {
        var seed = await SeedAsync();
        var orderId = await CreateDraftAsync(SaleInput(
            seed,
            (seed.FirstProductId, 1),
            (seed.SecondProductId, 1)));

        await using (var priceContext = CreateDbContext())
        {
            var firstProduct = await priceContext.Products.FindAsync(seed.FirstProductId);
            Assert.NotNull(firstProduct);
            firstProduct.Price = 99.00m;
            await priceContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateDbContext())
        {
            var service = CreateService(updateContext);
            var result = await service.UpdateDraftAsync(
                orderId,
                PurchaseInput(
                    seed,
                    (seed.FirstProductId, 2),
                    (seed.ThirdProductId, 3)));

            var updated = AssertSuccess(result);
            Assert.Equal(OrderStatus.Draft, updated.Status);
            Assert.Equal(34.00m, updated.TotalAmount);
        }

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == orderId);

        Assert.Equal(OrderType.Purchase, order.Type);
        Assert.Null(order.CustomerId);
        Assert.Equal(seed.SupplierId, order.SupplierId);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(
            12.50m,
            order.Items.Single(item => item.ProductId == seed.FirstProductId).UnitPrice);
        Assert.Equal(
            3.00m,
            order.Items.Single(item => item.ProductId == seed.ThirdProductId).UnitPrice);
        Assert.DoesNotContain(order.Items, item => item.ProductId == seed.SecondProductId);
        Assert.Empty(await verificationContext.StockMovements.ToListAsync());
        Assert.Equal(10, await verificationContext.Products
            .Where(product => product.Id == seed.FirstProductId)
            .Select(product => product.StockQuantity)
            .SingleAsync());
    }

    [Fact]
    public async Task ConfirmDraftAsync_ForPurchase_IncreasesStockAndCreatesStockInMovements()
    {
        var seed = await SeedAsync();
        var orderId = await CreateDraftAsync(PurchaseInput(
            seed,
            (seed.FirstProductId, 3),
            (seed.SecondProductId, 4)));

        await using (var confirmContext = CreateDbContext())
        {
            var result = await CreateService(confirmContext).ConfirmDraftAsync(orderId);
            var confirmed = AssertSuccess(result);

            Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
            Assert.Equal(66.50m, confirmed.TotalAmount);
        }

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(candidate => candidate.Id == orderId);
        var products = await verificationContext.Products.OrderBy(product => product.Id).ToListAsync();
        var movements = await verificationContext.StockMovements
            .Where(movement => movement.OrderId == orderId)
            .OrderBy(movement => movement.ProductId)
            .ToListAsync();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(13, products.Single(product => product.Id == seed.FirstProductId).StockQuantity);
        Assert.Equal(9, products.Single(product => product.Id == seed.SecondProductId).StockQuantity);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, movement =>
        {
            Assert.Equal(StockMovementType.StockIn, movement.Type);
            Assert.Equal(FixedUtcNow.UtcDateTime, movement.MovementDate);
            Assert.Contains(order.OrderNumber, movement.Description, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ConfirmDraftAsync_ForSale_DecreasesStockAndCreatesStockOutMovements()
    {
        var seed = await SeedAsync();
        var orderId = await CreateDraftAsync(SaleInput(
            seed,
            (seed.FirstProductId, 4),
            (seed.SecondProductId, 2)));

        await using (var confirmContext = CreateDbContext())
        {
            var result = await CreateService(confirmContext).ConfirmDraftAsync(orderId);
            Assert.Equal(OrderStatus.Confirmed, AssertSuccess(result).Status);
        }

        await using var verificationContext = CreateDbContext();
        var products = await verificationContext.Products.OrderBy(product => product.Id).ToListAsync();
        var movements = await verificationContext.StockMovements
            .Where(movement => movement.OrderId == orderId)
            .ToListAsync();

        Assert.Equal(6, products.Single(product => product.Id == seed.FirstProductId).StockQuantity);
        Assert.Equal(3, products.Single(product => product.Id == seed.SecondProductId).StockQuantity);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, movement => Assert.Equal(StockMovementType.StockOut, movement.Type));
    }

    [Fact]
    public async Task ConfirmDraftAsync_WhenOneSaleItemHasInsufficientStock_LeavesNoPartialChanges()
    {
        var seed = await SeedAsync(secondProductStock: 1);
        var orderId = await CreateDraftAsync(SaleInput(
            seed,
            (seed.FirstProductId, 2),
            (seed.SecondProductId, 2)));

        await using (var confirmContext = CreateDbContext())
        {
            var result = await CreateService(confirmContext).ConfirmDraftAsync(orderId);
            AssertFailure(
                result,
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.InsufficientStock);
        }

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(candidate => candidate.Id == orderId);
        var products = await verificationContext.Products.OrderBy(product => product.Id).ToListAsync();

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(10, products.Single(product => product.Id == seed.FirstProductId).StockQuantity);
        Assert.Equal(1, products.Single(product => product.Id == seed.SecondProductId).StockQuantity);
        Assert.False(await verificationContext.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task ConfirmDraftAsync_WhenPersistenceFailsAfterSave_RollsBackAllChanges()
    {
        var seed = await SeedAsync();
        var orderId = await CreateDraftAsync(PurchaseInput(
            seed,
            (seed.FirstProductId, 3),
            (seed.SecondProductId, 2)));

        await using (var failingContext = CreateDbContext(new ThrowAfterSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);
            await Assert.ThrowsAsync<TestPersistenceException>(
                () => service.ConfirmDraftAsync(orderId));
            Assert.Empty(failingContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(candidate => candidate.Id == orderId);
        var products = await verificationContext.Products.OrderBy(product => product.Id).ToListAsync();

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(10, products.Single(product => product.Id == seed.FirstProductId).StockQuantity);
        Assert.Equal(5, products.Single(product => product.Id == seed.SecondProductId).StockQuantity);
        Assert.False(await verificationContext.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task CancelDraftAsync_MakesOrderTerminalWithoutChangingStock()
    {
        var seed = await SeedAsync();
        var input = SaleInput(seed, (seed.FirstProductId, 2));
        var orderId = await CreateDraftAsync(input);

        await using (var serviceContext = CreateDbContext())
        {
            var service = CreateService(serviceContext);
            Assert.Equal(
                OrderStatus.Cancelled,
                AssertSuccess(await service.CancelDraftAsync(orderId)).Status);

            AssertFailure(
                await service.UpdateDraftAsync(orderId, input),
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft);
            AssertFailure(
                await service.ConfirmDraftAsync(orderId),
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft);
            AssertFailure(
                await service.CancelDraftAsync(orderId),
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft);
            AssertFailure(
                await service.DeleteDraftAsync(orderId),
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft);
        }

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            OrderStatus.Cancelled,
            await verificationContext.Orders
                .Where(order => order.Id == orderId)
                .Select(order => order.Status)
                .SingleAsync());
        Assert.Equal(
            10,
            await verificationContext.Products
                .Where(product => product.Id == seed.FirstProductId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
        Assert.False(await verificationContext.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task ConfirmedOrder_RejectsUpdateConfirmCancelAndDelete()
    {
        var seed = await SeedAsync();
        var input = SaleInput(seed, (seed.FirstProductId, 2));
        var orderId = await CreateDraftAsync(input);

        await using (var confirmContext = CreateDbContext())
        {
            Assert.Equal(
                OrderStatus.Confirmed,
                AssertSuccess(await CreateService(confirmContext).ConfirmDraftAsync(orderId)).Status);
        }

        await using var serviceContext = CreateDbContext();
        var service = CreateService(serviceContext);
        AssertFailure(
            await service.UpdateDraftAsync(orderId, input),
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft);
        AssertFailure(
            await service.ConfirmDraftAsync(orderId),
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft);
        AssertFailure(
            await service.CancelDraftAsync(orderId),
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft);
        AssertFailure(
            await service.DeleteDraftAsync(orderId),
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft);
    }

    [Fact]
    public async Task DeleteDraftAsync_RemovesOrderAndItems()
    {
        var seed = await SeedAsync();
        var orderId = await CreateDraftAsync(SaleInput(
            seed,
            (seed.FirstProductId, 2),
            (seed.SecondProductId, 1)));

        await using (var deleteContext = CreateDbContext())
        {
            var result = await CreateService(deleteContext).DeleteDraftAsync(orderId);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Orders.AnyAsync(order => order.Id == orderId));
        Assert.False(await verificationContext.OrderItems.AnyAsync(item => item.OrderId == orderId));
    }

    [Fact]
    public async Task CreateDraftAsync_ReturnsCategorizedErrorsForInvalidInput()
    {
        var seed = await SeedAsync();

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var invalidParty = SaleInput(seed, (seed.FirstProductId, 1));
        invalidParty.SupplierId = seed.SupplierId;
        AssertFailure(
            await service.CreateDraftAsync(invalidParty, CreatorUserId),
            ServiceErrorCategory.Validation,
            OrderServiceErrorCodes.InvalidParty);

        AssertFailure(
            await service.CreateDraftAsync(
                SaleInput(
                    seed,
                    (seed.FirstProductId, 1),
                    (seed.FirstProductId, 2)),
                CreatorUserId),
            ServiceErrorCategory.Validation,
            OrderServiceErrorCodes.DuplicateProduct);

        AssertFailure(
            await service.CreateDraftAsync(
                SaleInput(seed, (seed.FirstProductId, 0)),
                CreatorUserId),
            ServiceErrorCategory.Validation,
            OrderServiceErrorCodes.InvalidQuantity);

        AssertFailure(
            await service.CreateDraftAsync(
                SaleInput(seed, (int.MaxValue, 1)),
                CreatorUserId),
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.ProductNotFound);

        AssertFailure(
            await service.CreateDraftAsync(
                SaleInput(seed, (seed.FirstProductId, 1)),
                "missing-user"),
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.UserNotFound);
    }

    private async Task<SeedData> SeedAsync(int secondProductStock = 5)
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "Service Test Category" };
        var customer = new Customer { Name = "Service Test Customer" };
        var supplier = new Supplier { CompanyName = "Service Test Supplier" };
        var firstProduct = new Product
        {
            Name = "First Product",
            Sku = $"FIRST-{Guid.NewGuid():N}",
            Price = 12.50m,
            StockQuantity = 10,
            MinimumStockQuantity = 2,
            Category = category
        };
        var secondProduct = new Product
        {
            Name = "Second Product",
            Sku = $"SECOND-{Guid.NewGuid():N}",
            Price = 7.25m,
            StockQuantity = secondProductStock,
            MinimumStockQuantity = 1,
            Category = category
        };
        var thirdProduct = new Product
        {
            Name = "Third Product",
            Sku = $"THIRD-{Guid.NewGuid():N}",
            Price = 3.00m,
            StockQuantity = 8,
            MinimumStockQuantity = 1,
            Category = category
        };
        var user = new ApplicationUser
        {
            Id = CreatorUserId,
            UserName = "order-service-user",
            NormalizedUserName = "ORDER-SERVICE-USER",
            Email = "order-service@stockflow.test",
            NormalizedEmail = "ORDER-SERVICE@STOCKFLOW.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        dbContext.AddRange(customer, supplier, firstProduct, secondProduct, thirdProduct, user);
        await dbContext.SaveChangesAsync();

        return new SeedData(
            customer.Id,
            supplier.Id,
            firstProduct.Id,
            secondProduct.Id,
            thirdProduct.Id);
    }

    private async Task<int> CreateDraftAsync(OrderDraftInputModel input)
    {
        await using var dbContext = CreateDbContext();
        var result = await CreateService(dbContext).CreateDraftAsync(input, CreatorUserId);
        return AssertSuccess(result).OrderId;
    }

    private static OrderService CreateService(ApplicationDbContext dbContext)
    {
        return new OrderService(
            dbContext,
            NullLogger<OrderService>.Instance,
            new FixedTimeProvider());
    }

    private static OrderDraftInputModel SaleInput(
        SeedData seed,
        params (int ProductId, int Quantity)[] items)
    {
        return DraftInput(OrderType.Sale, seed.CustomerId, null, items);
    }

    private static OrderDraftInputModel PurchaseInput(
        SeedData seed,
        params (int ProductId, int Quantity)[] items)
    {
        return DraftInput(OrderType.Purchase, null, seed.SupplierId, items);
    }

    private static OrderDraftInputModel DraftInput(
        OrderType type,
        int? customerId,
        int? supplierId,
        params (int ProductId, int Quantity)[] items)
    {
        return new OrderDraftInputModel
        {
            Type = type,
            CustomerId = customerId,
            SupplierId = supplierId,
            Items = items
                .Select(item => new OrderItemInputModel
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }

    private static OrderMutationResult AssertSuccess(
        ServiceResult<OrderMutationResult> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<OrderMutationResult>(result.Value);
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

    private static void AssertFailure(
        ServiceResult result,
        ServiceErrorCategory category,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(category, result.Error.Category);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record SeedData(
        int CustomerId,
        int SupplierId,
        int FirstProductId,
        int SecondProductId,
        int ThirdProductId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return FixedUtcNow;
        }
    }

    private sealed class ThrowAfterSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            throw new TestPersistenceException();
        }
    }

    private sealed class TestPersistenceException : Exception;
}
