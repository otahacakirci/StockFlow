using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Categories;
using StockFlow.Services.Common;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Tests.Services;

public sealed class CategoryServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesSearchSortPaginationAndProjectionWithoutTracking()
    {
        await SeedCategoryAsync("Office", productCount: 1);
        await SeedCategoryAsync("Office Supplies", productCount: 2);
        await SeedCategoryAsync("Warehouse");

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var descending = AssertSuccess(await service.GetListAsync(new CategoryListQueryModel
        {
            SearchTerm = "  office  ",
            SortOrder = CategorySortOrder.NameDescending,
            Page = 1,
            PageSize = 1
        }));

        Assert.Equal("office", descending.SearchTerm);
        Assert.Equal(CategorySortOrder.NameDescending, descending.SortOrder);
        Assert.Equal(1, descending.Page);
        Assert.Equal(1, descending.PageSize);
        Assert.Equal(2, descending.TotalCount);
        Assert.Equal(2, descending.TotalPages);
        var firstItem = Assert.Single(descending.Items);
        Assert.Equal("Office Supplies", firstItem.Name);
        Assert.Equal(2, firstItem.ProductCount);

        var ascendingSecondPage = AssertSuccess(await service.GetListAsync(new CategoryListQueryModel
        {
            SearchTerm = "office",
            SortOrder = CategorySortOrder.NameAscending,
            Page = 2,
            PageSize = 1
        }));

        Assert.Equal("Office Supplies", Assert.Single(ascendingSecondPage.Items).Name);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidPagingAndSortValues()
    {
        await SeedCategoryAsync("Beta");
        await SeedCategoryAsync("Alpha");

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var capped = AssertSuccess(await service.GetListAsync(new CategoryListQueryModel
        {
            SearchTerm = "   ",
            SortOrder = (CategorySortOrder)int.MaxValue,
            Page = int.MaxValue,
            PageSize = int.MaxValue
        }));

        Assert.Null(capped.SearchTerm);
        Assert.Equal(CategorySortOrder.NameAscending, capped.SortOrder);
        Assert.Equal(1, capped.Page);
        Assert.Equal(100, capped.PageSize);
        Assert.Equal(1, capped.TotalPages);
        Assert.Equal(["Alpha", "Beta"], capped.Items.Select(item => item.Name));

        var defaults = AssertSuccess(await service.GetListAsync(new CategoryListQueryModel
        {
            Page = -1,
            PageSize = 0
        }));

        Assert.Equal(1, defaults.Page);
        Assert.Equal(20, defaults.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProjectionAndNotFoundError()
    {
        var categoryId = await SeedCategoryAsync("Tracked Category", productCount: 2);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var category = AssertSuccess(await service.GetByIdAsync(categoryId));

        Assert.Equal(categoryId, category.Id);
        Assert.Equal("Tracked Category", category.Name);
        Assert.Equal(2, category.ProductCount);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            CategoryServiceErrorCodes.CategoryNotFound);
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndReturnsValidationErrors()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(
            await service.CreateAsync(null),
            ServiceErrorCategory.Validation,
            CategoryServiceErrorCodes.InputRequired);
        AssertFailure(
            await service.CreateAsync(new CategoryInputModel { Name = "   " }),
            ServiceErrorCategory.Validation,
            CategoryServiceErrorCodes.NameRequired);
        AssertFailure(
            await service.CreateAsync(new CategoryInputModel { Name = new string('x', 101) }),
            ServiceErrorCategory.Validation,
            CategoryServiceErrorCodes.NameTooLong);

        var created = AssertSuccess(await service.CreateAsync(new CategoryInputModel
        {
            Name = "  Consumables  "
        }));

        Assert.True(created.Id > 0);
        Assert.Equal("Consumables", created.Name);
        Assert.Equal(0, created.ProductCount);

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            "Consumables",
            await verificationContext.Categories
                .Where(category => category.Id == created.Id)
                .Select(category => category.Name)
                .SingleAsync());
        Assert.Equal(1, await verificationContext.Categories.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingAndSeparatesValidationFromNotFound()
    {
        var categoryId = await SeedCategoryAsync("Original", productCount: 1);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var updated = AssertSuccess(await service.UpdateAsync(
            categoryId,
            new CategoryInputModel { Name = "  Updated  " }));

        Assert.Equal("Updated", updated.Name);
        Assert.Equal(1, updated.ProductCount);

        AssertFailure(
            await service.UpdateAsync(categoryId, new CategoryInputModel { Name = " " }),
            ServiceErrorCategory.Validation,
            CategoryServiceErrorCodes.NameRequired);
        AssertFailure(
            await service.UpdateAsync(
                int.MaxValue,
                new CategoryInputModel { Name = "Missing" }),
            ServiceErrorCategory.NotFound,
            CategoryServiceErrorCodes.CategoryNotFound);

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            "Updated",
            await verificationContext.Categories
                .Where(category => category.Id == categoryId)
                .Select(category => category.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenCategoryHasNoProducts_DeletesCategory()
    {
        var categoryId = await SeedCategoryAsync("Disposable");

        await using (var dbContext = CreateDbContext())
        {
            var result = await CreateService(dbContext).DeleteAsync(categoryId);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Categories.AnyAsync(
            category => category.Id == categoryId));
    }

    [Fact]
    public async Task DeleteAsync_WhenCategoryHasProducts_ReturnsBusinessRuleAndPreservesCategory()
    {
        var categoryId = await SeedCategoryAsync("Protected", productCount: 1);

        await using (var dbContext = CreateDbContext())
        {
            AssertFailure(
                await CreateService(dbContext).DeleteAsync(categoryId),
                ServiceErrorCategory.BusinessRule,
                CategoryServiceErrorCodes.CategoryHasProducts);
        }

        await using var verificationContext = CreateDbContext();
        Assert.True(await verificationContext.Categories.AnyAsync(
            category => category.Id == categoryId));
        Assert.True(await verificationContext.Products.AnyAsync(
            product => product.CategoryId == categoryId));
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_RethrowsAndClearsTracker()
    {
        await using (var failingContext = CreateDbContext(new ThrowBeforeSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);

            await Assert.ThrowsAsync<TestPersistenceException>(() => service.CreateAsync(
                new CategoryInputModel { Name = "Persistence Failure" }));
            Assert.Empty(failingContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Categories.AnyAsync(
            category => category.Name == "Persistence Failure"));
    }

    private async Task<int> SeedCategoryAsync(string name, int productCount = 0)
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = name };

        for (var index = 0; index < productCount; index++)
        {
            category.Products.Add(new Product
            {
                Name = $"{name} Product {index + 1}",
                Sku = $"CATEGORY-{Guid.NewGuid():N}",
                Price = 1.00m,
                StockQuantity = 0,
                MinimumStockQuantity = 0
            });
        }

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category.Id;
    }

    private static CategoryService CreateService(ApplicationDbContext dbContext)
    {
        return new CategoryService(
            dbContext,
            NullLogger<CategoryService>.Instance);
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
