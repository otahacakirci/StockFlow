using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Products;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Products;

namespace StockFlow.Tests.Services;

public sealed class ProductServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesSearchFiltersSortPaginationAndProjectionWithoutTracking()
    {
        var categories = await SeedCategoriesAsync();
        await SeedProductAsync(categories.FirstId, "Paper", "OFF-PAPER", 5.00m, 2, 5);
        await SeedProductAsync(categories.FirstId, "Pen", "OFF-PEN", 10.00m, 0, 2);
        await SeedProductAsync(categories.SecondId, "Shelf", "WH-SHELF", 100.00m, 1, 1);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = AssertSuccess(await service.GetListAsync(new ProductListQueryModel
        {
            SearchTerm = "  off  ",
            CategoryId = categories.FirstId,
            LowStockOnly = true,
            SortOrder = ProductSortOrder.PriceDescending,
            Page = 1,
            PageSize = 1
        }));

        Assert.Equal("off", result.SearchTerm);
        Assert.Equal(categories.FirstId, result.CategoryId);
        Assert.True(result.LowStockOnly);
        Assert.Equal(ProductSortOrder.PriceDescending, result.SortOrder);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        var item = Assert.Single(result.Items);
        Assert.Equal("Pen", item.Name);
        Assert.Equal("Primary", item.CategoryName);
        Assert.True(item.IsLowStock);

        var nameSearch = AssertSuccess(await service.GetListAsync(new ProductListQueryModel
        {
            SearchTerm = "shelf"
        }));
        Assert.Equal("Shelf", Assert.Single(nameSearch.Items).Name);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidValuesAndHandlesEmptyResults()
    {
        var categories = await SeedCategoriesAsync();
        await SeedProductAsync(categories.FirstId, "Beta", "BETA", 2.00m, 1, 0);
        await SeedProductAsync(categories.FirstId, "Alpha", "ALPHA", 1.00m, 1, 0);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var normalized = AssertSuccess(await service.GetListAsync(new ProductListQueryModel
        {
            SearchTerm = "   ",
            CategoryId = 0,
            SortOrder = (ProductSortOrder)int.MaxValue,
            Page = int.MaxValue,
            PageSize = int.MaxValue
        }));

        Assert.Null(normalized.SearchTerm);
        Assert.Null(normalized.CategoryId);
        Assert.Equal(ProductSortOrder.NameAscending, normalized.SortOrder);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Equal(["Alpha", "Beta"], normalized.Items.Select(item => item.Name));

        var empty = AssertSuccess(await service.GetListAsync(new ProductListQueryModel
        {
            SearchTerm = "missing",
            Page = -1,
            PageSize = 0
        }));
        Assert.Equal(1, empty.Page);
        Assert.Equal(20, empty.PageSize);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCategoryProjectionAndNotFoundError()
    {
        var categories = await SeedCategoriesAsync();
        var productId = await SeedProductAsync(
            categories.FirstId,
            "Detail Product",
            "DETAIL",
            3.50m,
            2,
            2);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var product = AssertSuccess(await service.GetByIdAsync(productId));
        Assert.Equal("Primary", product.CategoryName);
        Assert.True(product.IsLowStock);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            ProductServiceErrorCodes.ProductNotFound);
    }

    [Fact]
    public async Task CreateAsync_CreatesNormalizedProductWithInitialStockWithoutMovement()
    {
        var categories = await SeedCategoriesAsync();

        await using var dbContext = CreateDbContext();
        var created = AssertSuccess(await CreateService(dbContext).CreateAsync(new ProductCreateInputModel
        {
            Name = "  Opening Product  ",
            Sku = "  Open-001  ",
            Price = 12.30m,
            StockQuantity = 5,
            MinimumStockQuantity = 5,
            CategoryId = categories.FirstId
        }));

        Assert.Equal("Opening Product", created.Name);
        Assert.Equal("Open-001", created.Sku);
        Assert.Equal(5, created.StockQuantity);
        Assert.Equal("Primary", created.CategoryName);
        Assert.True(created.IsLowStock);

        await using var verificationContext = CreateDbContext();
        Assert.Equal(5, await verificationContext.Products
            .Where(product => product.Id == created.Id)
            .Select(product => product.StockQuantity)
            .SingleAsync());
        Assert.False(await verificationContext.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorsForInvalidInput()
    {
        var categories = await SeedCategoriesAsync();

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(await service.CreateAsync(null), ServiceErrorCategory.Validation, ProductServiceErrorCodes.InputRequired);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, name: " ")), ServiceErrorCategory.Validation, ProductServiceErrorCodes.NameRequired);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, name: new string('n', 151))), ServiceErrorCategory.Validation, ProductServiceErrorCodes.NameTooLong);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, sku: " ")), ServiceErrorCategory.Validation, ProductServiceErrorCodes.SkuRequired);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, sku: new string('s', 65))), ServiceErrorCategory.Validation, ProductServiceErrorCodes.SkuTooLong);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, price: 0)), ServiceErrorCategory.Validation, ProductServiceErrorCodes.PriceInvalid);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, price: 1.234m)), ServiceErrorCategory.Validation, ProductServiceErrorCodes.PriceInvalid);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, stockQuantity: -1)), ServiceErrorCategory.Validation, ProductServiceErrorCodes.StockQuantityInvalid);
        AssertFailure(await service.CreateAsync(ValidCreate(categories.FirstId, minimumStockQuantity: -1)), ServiceErrorCategory.Validation, ProductServiceErrorCodes.MinimumStockQuantityInvalid);
        AssertFailure(await service.CreateAsync(ValidCreate(categoryId: 0)), ServiceErrorCategory.Validation, ProductServiceErrorCodes.CategoryInvalid);

        Assert.False(await dbContext.Products.AnyAsync());
    }

    [Fact]
    public async Task CreateAsync_SeparatesMissingCategoryAndDuplicateSkuAndDatabaseConstraintProtectsSku()
    {
        var categories = await SeedCategoriesAsync();

        await using (var serviceContext = CreateDbContext())
        {
            var service = CreateService(serviceContext);
            AssertFailure(
                await service.CreateAsync(ValidCreate(int.MaxValue)),
                ServiceErrorCategory.NotFound,
                ProductServiceErrorCodes.CategoryNotFound);

            AssertSuccess(await service.CreateAsync(ValidCreate(categories.FirstId, sku: "SKU-ONE")));
            AssertFailure(
                await service.CreateAsync(ValidCreate(categories.FirstId, sku: "sku-one")),
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.SkuDuplicate);
        }

        await using var constraintContext = CreateDbContext();
        constraintContext.Products.Add(new Product
        {
            Name = "Constraint Duplicate",
            Sku = "SKU-ONE",
            Price = 1.00m,
            StockQuantity = 0,
            MinimumStockQuantity = 0,
            CategoryId = categories.FirstId
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => constraintContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangesEditableFieldsAndPreservesStockAndOrderPriceSnapshot()
    {
        var categories = await SeedCategoriesAsync();
        var productId = await SeedProductAsync(
            categories.FirstId,
            "Original",
            "ORIGINAL",
            5.00m,
            7,
            1);
        var orderId = await SeedDraftOrderAsync(productId, unitPrice: 5.00m);

        await using (var updateContext = CreateDbContext())
        {
            var updated = AssertSuccess(await CreateService(updateContext).UpdateAsync(
                productId,
                new ProductUpdateInputModel
                {
                    Name = "  Updated  ",
                    Sku = "  UPDATED  ",
                    Price = 9.00m,
                    MinimumStockQuantity = 8,
                    CategoryId = categories.SecondId
                }));

            Assert.Equal(7, updated.StockQuantity);
            Assert.Equal(9.00m, updated.Price);
            Assert.Equal("Secondary", updated.CategoryName);
            Assert.True(updated.IsLowStock);
        }

        await using var verificationContext = CreateDbContext();
        var product = await verificationContext.Products.SingleAsync(candidate => candidate.Id == productId);
        Assert.Equal("Updated", product.Name);
        Assert.Equal("UPDATED", product.Sku);
        Assert.Equal(7, product.StockQuantity);
        Assert.Equal(8, product.MinimumStockQuantity);
        Assert.Equal(categories.SecondId, product.CategoryId);
        Assert.Equal(5.00m, await verificationContext.OrderItems
            .Where(item => item.OrderId == orderId && item.ProductId == productId)
            .Select(item => item.UnitPrice)
            .SingleAsync());
        Assert.False(await verificationContext.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_SeparatesMissingProductCategoryAndDuplicateSku()
    {
        var categories = await SeedCategoriesAsync();
        var firstProductId = await SeedProductAsync(categories.FirstId, "First", "FIRST", 1.00m, 0, 0);
        await SeedProductAsync(categories.FirstId, "Second", "SECOND", 1.00m, 0, 0);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(
            await service.UpdateAsync(int.MaxValue, ValidUpdate(categories.FirstId)),
            ServiceErrorCategory.NotFound,
            ProductServiceErrorCodes.ProductNotFound);
        AssertFailure(
            await service.UpdateAsync(firstProductId, null),
            ServiceErrorCategory.Validation,
            ProductServiceErrorCodes.InputRequired);
        AssertFailure(
            await service.UpdateAsync(firstProductId, ValidUpdate(int.MaxValue)),
            ServiceErrorCategory.NotFound,
            ProductServiceErrorCodes.CategoryNotFound);
        AssertFailure(
            await service.UpdateAsync(firstProductId, ValidUpdate(categories.FirstId, sku: "second")),
            ServiceErrorCategory.Validation,
            ProductServiceErrorCodes.SkuDuplicate);
    }

    [Fact]
    public async Task DeleteAsync_DeletesHistorylessProductEvenWhenStockIsNonzeroAndReturnsNotFoundAfterward()
    {
        var categories = await SeedCategoriesAsync();
        var productId = await SeedProductAsync(categories.FirstId, "Disposable", "DISPOSABLE", 1.00m, 9, 0);

        await using (var deleteContext = CreateDbContext())
        {
            var service = CreateService(deleteContext);
            var deleted = await service.DeleteAsync(productId);
            Assert.True(deleted.IsSuccess);
            Assert.Null(deleted.Error);

            AssertFailure(
                await service.DeleteAsync(productId),
                ServiceErrorCategory.NotFound,
                ProductServiceErrorCodes.ProductNotFound);
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Products.AnyAsync(product => product.Id == productId));
    }

    [Fact]
    public async Task DeleteAsync_WhenOrderItemExists_ReturnsBusinessRuleAndPreservesProduct()
    {
        var categories = await SeedCategoriesAsync();
        var productId = await SeedProductAsync(categories.FirstId, "Ordered", "ORDERED", 2.00m, 4, 1);
        await SeedDraftOrderAsync(productId, unitPrice: 2.00m);

        await using (var deleteContext = CreateDbContext())
        {
            AssertFailure(
                await CreateService(deleteContext).DeleteAsync(productId),
                ServiceErrorCategory.BusinessRule,
                ProductServiceErrorCodes.ProductHasHistory);
        }

        await using var verificationContext = CreateDbContext();
        Assert.True(await verificationContext.Products.AnyAsync(product => product.Id == productId));
    }

    [Fact]
    public async Task DeleteAsync_WhenOnlyStockMovementExists_ReturnsBusinessRuleAndPreservesProduct()
    {
        var categories = await SeedCategoriesAsync();
        var productId = await SeedProductAsync(categories.FirstId, "Moved", "MOVED", 2.00m, 4, 1);
        await SeedStockMovementAsync(productId);

        await using (var deleteContext = CreateDbContext())
        {
            AssertFailure(
                await CreateService(deleteContext).DeleteAsync(productId),
                ServiceErrorCategory.BusinessRule,
                ProductServiceErrorCodes.ProductHasHistory);
        }

        await using var verificationContext = CreateDbContext();
        Assert.True(await verificationContext.Products.AnyAsync(product => product.Id == productId));
        Assert.False(await verificationContext.OrderItems.AnyAsync(item => item.ProductId == productId));
        Assert.True(await verificationContext.StockMovements.AnyAsync(movement => movement.ProductId == productId));
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_RethrowsAndClearsTracker()
    {
        var categories = await SeedCategoriesAsync();

        await using (var failingContext = CreateDbContext(new ThrowBeforeSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);
            await Assert.ThrowsAsync<TestPersistenceException>(() => service.CreateAsync(
                ValidCreate(categories.FirstId, sku: "PERSISTENCE-FAILURE")));
            Assert.Empty(failingContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Products.AnyAsync(
            product => product.Sku == "PERSISTENCE-FAILURE"));
    }

    private async Task<CategorySeed> SeedCategoriesAsync()
    {
        await using var dbContext = CreateDbContext();
        var first = new Category { Name = "Primary" };
        var second = new Category { Name = "Secondary" };
        dbContext.Categories.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        return new CategorySeed(first.Id, second.Id);
    }

    private async Task<int> SeedProductAsync(
        int categoryId,
        string name,
        string sku,
        decimal price,
        int stockQuantity,
        int minimumStockQuantity)
    {
        await using var dbContext = CreateDbContext();
        var product = new Product
        {
            Name = name,
            Sku = sku,
            Price = price,
            StockQuantity = stockQuantity,
            MinimumStockQuantity = minimumStockQuantity,
            CategoryId = categoryId
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private async Task<int> SeedDraftOrderAsync(int productId, decimal unitPrice)
    {
        await using var dbContext = CreateDbContext();
        var order = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = OrderType.Sale,
            Status = OrderStatus.Draft,
            OrderDate = DateTime.UtcNow,
            TotalAmount = unitPrice,
            Customer = new Customer { Name = "Product Test Customer" },
            Items =
            [
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = unitPrice
                }
            ]
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task SeedStockMovementAsync(int productId)
    {
        await using var dbContext = CreateDbContext();
        var order = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = OrderType.Sale,
            Status = OrderStatus.Confirmed,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 1.00m,
            Customer = new Customer { Name = "Movement Test Customer" },
            StockMovements =
            [
                new StockMovement
                {
                    ProductId = productId,
                    Type = StockMovementType.StockOut,
                    Quantity = 1,
                    Description = "Product deletion history test",
                    MovementDate = DateTime.UtcNow
                }
            ]
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
    }

    private static ProductCreateInputModel ValidCreate(
        int categoryId,
        string name = "Valid Product",
        string sku = "VALID-SKU",
        decimal price = 1.00m,
        int stockQuantity = 0,
        int minimumStockQuantity = 0)
    {
        return new ProductCreateInputModel
        {
            Name = name,
            Sku = sku,
            Price = price,
            StockQuantity = stockQuantity,
            MinimumStockQuantity = minimumStockQuantity,
            CategoryId = categoryId
        };
    }

    private static ProductUpdateInputModel ValidUpdate(int categoryId, string sku = "UPDATED-SKU")
    {
        return new ProductUpdateInputModel
        {
            Name = "Updated Product",
            Sku = sku,
            Price = 2.00m,
            MinimumStockQuantity = 1,
            CategoryId = categoryId
        };
    }

    private static ProductService CreateService(ApplicationDbContext dbContext)
    {
        return new ProductService(dbContext, NullLogger<ProductService>.Instance);
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

    private sealed record CategorySeed(int FirstId, int SecondId);

    private sealed class ThrowBeforeSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new TestPersistenceException();
        }
    }

    private sealed class TestPersistenceException : Exception;
}
