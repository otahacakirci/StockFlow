using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Services.Categories;

/// <summary>
/// Category sorgularını, giriş doğrulamasını ve ilişki korumalı kalıcılaştırma akışını yönetir.
/// </summary>
internal sealed class CategoryService(
    ApplicationDbContext dbContext,
    ILogger<CategoryService> logger) : ICategoryService
{
    private const int MaximumNameLength = 100;

    public async Task<ServiceResult<CategoryListViewModel>> GetListAsync(
        CategoryListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var categories = dbContext.Categories.AsNoTracking();

        if (normalizedQuery.SearchTerm is not null)
        {
            categories = categories.Where(category =>
                category.Name.Contains(normalizedQuery.SearchTerm));
        }

        var totalCount = await categories.CountAsync(cancellationToken);
        var page = ListPagingPolicy.Resolve(normalizedQuery.PageRequest, totalCount);

        var orderedCategories = normalizedQuery.SortOrder == CategorySortOrder.NameDescending
            ? categories.OrderByDescending(category => category.Name).ThenByDescending(category => category.Id)
            : categories.OrderBy(category => category.Name).ThenBy(category => category.Id);

        var items = await orderedCategories
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(category => new CategoryViewModel(
                category.Id,
                category.Name,
                category.Products.Count))
            .ToListAsync(cancellationToken);

        return ServiceResult<CategoryListViewModel>.Success(new CategoryListViewModel(
            items,
            normalizedQuery.SearchTerm,
            normalizedQuery.SortOrder,
            page.Page,
            page.PageSize,
            totalCount,
            page.TotalPages));
    }

    public async Task<ServiceResult<CategoryViewModel>> GetByIdAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new CategoryViewModel(
                candidate.Id,
                candidate.Name,
                candidate.Products.Count))
            .SingleOrDefaultAsync(cancellationToken);

        return category is null
            ? CategoryNotFound<CategoryViewModel>()
            : ServiceResult<CategoryViewModel>.Success(category);
    }

    public async Task<ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Select(category => new CategorySelectionOptionViewModel(
                category.Id,
                category.Name))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>.Success(options);
    }

    public async Task<ServiceResult<CategoryViewModel>> CreateAsync(
        CategoryInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAndNormalizeName(input, out var normalizedName);
        if (validationError is not null)
        {
            logger.LogWarning(
                "Category creation rejected with error code {ErrorCode}.",
                validationError.Code);
            return ServiceResult<CategoryViewModel>.Failure(validationError);
        }

        var category = new Category { Name = normalizedName };
        dbContext.Categories.Add(category);
        await PersistChangesAsync("create", category, cancellationToken);

        logger.LogInformation("Category {CategoryId} created.", category.Id);
        return ServiceResult<CategoryViewModel>.Success(ToViewModel(category, productCount: 0));
    }

    public async Task<ServiceResult<CategoryViewModel>> UpdateAsync(
        int categoryId,
        CategoryInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return CategoryNotFound<CategoryViewModel>();
        }

        var validationError = ValidateAndNormalizeName(input, out var normalizedName);
        if (validationError is not null)
        {
            logger.LogWarning(
                "Category {CategoryId} update rejected with error code {ErrorCode}.",
                category.Id,
                validationError.Code);
            return ServiceResult<CategoryViewModel>.Failure(validationError);
        }

        var productCount = await dbContext.Products.CountAsync(
            product => product.CategoryId == category.Id,
            cancellationToken);

        category.Name = normalizedName;
        await PersistChangesAsync("update", category, cancellationToken);

        logger.LogInformation("Category {CategoryId} updated.", category.Id);
        return ServiceResult<CategoryViewModel>.Success(ToViewModel(category, productCount));
    }

    public async Task<ServiceResult> DeleteAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.NotFound,
                CategoryServiceErrorCodes.CategoryNotFound,
                "Kategori bulunamadı."));
        }

        if (await dbContext.Products.AnyAsync(
                product => product.CategoryId == category.Id,
                cancellationToken))
        {
            logger.LogWarning(
                "Category {CategoryId} deletion rejected because products exist.",
                category.Id);
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                CategoryServiceErrorCodes.CategoryHasProducts,
                "Bağlı ürünü bulunan kategori fiziksel olarak silinemez."));
        }

        dbContext.Categories.Remove(category);
        await PersistChangesAsync("delete", category, cancellationToken);

        logger.LogInformation("Category {CategoryId} deleted.", categoryId);
        return ServiceResult.Success();
    }

    private async Task PersistChangesAsync(
        string operation,
        Category category,
        CancellationToken cancellationToken)
    {
        await TrackedPersistence.SaveChangesAsync(
            dbContext,
            exception => logger.LogError(
                exception,
                "Category persistence operation {Operation} failed for category {CategoryId}.",
                operation,
                category.Id),
            cancellationToken);
    }

    private static NormalizedCategoryListQuery NormalizeQuery(CategoryListQueryModel? query)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query?.SearchTerm)
            ? null
            : query.SearchTerm.Trim();
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : CategorySortOrder.NameAscending;
        var pageRequest = ListPagingPolicy.Normalize(query?.Page, query?.PageSize);

        return new NormalizedCategoryListQuery(searchTerm, sortOrder, pageRequest);
    }

    private static ServiceError? ValidateAndNormalizeName(
        CategoryInputModel? input,
        out string normalizedName)
    {
        normalizedName = string.Empty;

        if (input is null)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CategoryServiceErrorCodes.InputRequired,
                "Kategori bilgileri zorunludur.");
        }

        normalizedName = input.Name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CategoryServiceErrorCodes.NameRequired,
                "Kategori adı zorunludur.");
        }

        if (normalizedName.Length > MaximumNameLength)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CategoryServiceErrorCodes.NameTooLong,
                $"Kategori adı en fazla {MaximumNameLength} karakter olabilir.");
        }

        return null;
    }

    private static CategoryViewModel ToViewModel(Category category, int productCount)
    {
        return new CategoryViewModel(category.Id, category.Name, productCount);
    }

    private static ServiceResult<T> CategoryNotFound<T>()
    {
        return ServiceResult<T>.Failure(CreateError(
            ServiceErrorCategory.NotFound,
            CategoryServiceErrorCodes.CategoryNotFound,
            "Kategori bulunamadı."));
    }

    private static ServiceError CreateError(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return new ServiceError(category, code, message);
    }

    private sealed record NormalizedCategoryListQuery(
        string? SearchTerm,
        CategorySortOrder SortOrder,
        NormalizedPageRequest PageRequest);
}
