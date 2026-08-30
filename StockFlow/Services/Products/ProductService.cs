using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Products;

namespace StockFlow.Services.Products;

/// <summary>
/// Product sorgularını, giriş doğrulamasını ve geçmiş korumalı kalıcılaştırma akışını yönetir.
/// </summary>
internal sealed class ProductService(
    ApplicationDbContext dbContext,
    ILogger<ProductService> logger) : IProductService
{
    private const int MaximumNameLength = 150;
    private const int MaximumSkuLength = 64;
    private const decimal MaximumDatabaseAmount = 9_999_999_999_999_999.99m;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<ServiceResult<ProductListViewModel>> GetListAsync(
        ProductListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var products = dbContext.Products.AsNoTracking();

        if (normalizedQuery.SearchTerm is not null)
        {
            products = products.Where(product =>
                product.Name.Contains(normalizedQuery.SearchTerm)
                || product.Sku.Contains(normalizedQuery.SearchTerm));
        }

        if (normalizedQuery.CategoryId.HasValue)
        {
            products = products.Where(product =>
                product.CategoryId == normalizedQuery.CategoryId.Value);
        }

        if (normalizedQuery.LowStockOnly)
        {
            products = products.Where(product =>
                product.StockQuantity <= product.MinimumStockQuantity);
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling((double)totalCount / normalizedQuery.PageSize);
        var page = totalPages == 0
            ? 1
            : Math.Min(normalizedQuery.Page, totalPages);

        var orderedProducts = ApplySort(products, normalizedQuery.SortOrder);
        var items = await orderedProducts
            .Skip((page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .Select(product => new ProductViewModel(
                product.Id,
                product.Name,
                product.Sku,
                product.Price,
                product.StockQuantity,
                product.MinimumStockQuantity,
                product.CategoryId,
                product.Category.Name,
                product.StockQuantity <= product.MinimumStockQuantity))
            .ToListAsync(cancellationToken);

        return ServiceResult<ProductListViewModel>.Success(new ProductListViewModel(
            items,
            normalizedQuery.SearchTerm,
            normalizedQuery.CategoryId,
            normalizedQuery.LowStockOnly,
            normalizedQuery.SortOrder,
            page,
            normalizedQuery.PageSize,
            totalCount,
            totalPages));
    }

    public async Task<ServiceResult<ProductViewModel>> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Where(candidate => candidate.Id == productId)
            .Select(candidate => new ProductViewModel(
                candidate.Id,
                candidate.Name,
                candidate.Sku,
                candidate.Price,
                candidate.StockQuantity,
                candidate.MinimumStockQuantity,
                candidate.CategoryId,
                candidate.Category.Name,
                candidate.StockQuantity <= candidate.MinimumStockQuantity))
            .SingleOrDefaultAsync(cancellationToken);

        return product is null
            ? ProductNotFound<ProductViewModel>()
            : ServiceResult<ProductViewModel>.Success(product);
    }

    public async Task<ServiceResult<ProductViewModel>> CreateAsync(
        ProductCreateInputModel? input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return Rejected<ProductViewModel>(
                "create",
                CreateError(
                    ServiceErrorCategory.Validation,
                    ProductServiceErrorCodes.InputRequired,
                    "Ürün bilgileri zorunludur."));
        }

        var validationError = ValidateAndNormalizeInput(
            input.Name,
            input.Sku,
            input.Price,
            input.MinimumStockQuantity,
            input.CategoryId,
            out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<ProductViewModel>("create", validationError);
        }

        if (input.StockQuantity < 0)
        {
            return Rejected<ProductViewModel>(
                "create",
                CreateError(
                    ServiceErrorCategory.Validation,
                    ProductServiceErrorCodes.StockQuantityInvalid,
                    "Başlangıç stok miktarı sıfır veya pozitif olmalıdır."));
        }

        var categoryNameResult = await GetCategoryNameAsync(
            validatedInput.CategoryId,
            cancellationToken);
        if (!categoryNameResult.IsSuccess)
        {
            return ServiceResult<ProductViewModel>.Failure(categoryNameResult.Error!);
        }

        if (await SkuExistsAsync(validatedInput.Sku, excludedProductId: null, cancellationToken))
        {
            return Rejected<ProductViewModel>("create", DuplicateSkuError());
        }

        var product = new Product
        {
            Name = validatedInput.Name,
            Sku = validatedInput.Sku,
            Price = validatedInput.Price,
            StockQuantity = input.StockQuantity,
            MinimumStockQuantity = validatedInput.MinimumStockQuantity,
            CategoryId = validatedInput.CategoryId
        };

        dbContext.Products.Add(product);
        await PersistChangesAsync("create", product, cancellationToken);

        logger.LogInformation("Product {ProductId} created.", product.Id);
        return ServiceResult<ProductViewModel>.Success(ToViewModel(
            product,
            categoryNameResult.Value!));
    }

    public async Task<ServiceResult<ProductViewModel>> UpdateAsync(
        int productId,
        ProductUpdateInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound<ProductViewModel>();
        }

        if (input is null)
        {
            return Rejected<ProductViewModel>(
                "update",
                CreateError(
                    ServiceErrorCategory.Validation,
                    ProductServiceErrorCodes.InputRequired,
                    "Ürün bilgileri zorunludur."),
                product.Id);
        }

        var validationError = ValidateAndNormalizeInput(
            input.Name,
            input.Sku,
            input.Price,
            input.MinimumStockQuantity,
            input.CategoryId,
            out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<ProductViewModel>("update", validationError, product.Id);
        }

        var categoryNameResult = await GetCategoryNameAsync(
            validatedInput.CategoryId,
            cancellationToken);
        if (!categoryNameResult.IsSuccess)
        {
            return ServiceResult<ProductViewModel>.Failure(categoryNameResult.Error!);
        }

        if (await SkuExistsAsync(validatedInput.Sku, product.Id, cancellationToken))
        {
            return Rejected<ProductViewModel>("update", DuplicateSkuError(), product.Id);
        }

        product.Name = validatedInput.Name;
        product.Sku = validatedInput.Sku;
        product.Price = validatedInput.Price;
        product.MinimumStockQuantity = validatedInput.MinimumStockQuantity;
        product.CategoryId = validatedInput.CategoryId;

        await PersistChangesAsync("update", product, cancellationToken);

        logger.LogInformation("Product {ProductId} updated.", product.Id);
        return ServiceResult<ProductViewModel>.Success(ToViewModel(
            product,
            categoryNameResult.Value!));
    }

    public async Task<ServiceResult> DeleteAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult.Failure(ProductNotFoundError());
        }

        var hasOrderItems = await dbContext.OrderItems.AnyAsync(
            item => item.ProductId == product.Id,
            cancellationToken);
        var hasStockMovements = await dbContext.StockMovements.AnyAsync(
            movement => movement.ProductId == product.Id,
            cancellationToken);

        if (hasOrderItems || hasStockMovements)
        {
            logger.LogWarning(
                "Product {ProductId} deletion rejected because history exists. HasOrderItems: {HasOrderItems}; HasStockMovements: {HasStockMovements}.",
                product.Id,
                hasOrderItems,
                hasStockMovements);
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                ProductServiceErrorCodes.ProductHasHistory,
                "Sipariş kalemi veya stok hareketi geçmişi bulunan ürün fiziksel olarak silinemez."));
        }

        dbContext.Products.Remove(product);
        await PersistChangesAsync("delete", product, cancellationToken);

        logger.LogInformation("Product {ProductId} deleted.", productId);
        return ServiceResult.Success();
    }

    private static IOrderedQueryable<Product> ApplySort(
        IQueryable<Product> products,
        ProductSortOrder sortOrder)
    {
        return sortOrder switch
        {
            ProductSortOrder.NameDescending => products
                .OrderByDescending(product => product.Name)
                .ThenByDescending(product => product.Id),
            ProductSortOrder.PriceAscending => products
                .OrderBy(product => product.Price)
                .ThenBy(product => product.Id),
            ProductSortOrder.PriceDescending => products
                .OrderByDescending(product => product.Price)
                .ThenByDescending(product => product.Id),
            ProductSortOrder.StockQuantityAscending => products
                .OrderBy(product => product.StockQuantity)
                .ThenBy(product => product.Id),
            ProductSortOrder.StockQuantityDescending => products
                .OrderByDescending(product => product.StockQuantity)
                .ThenByDescending(product => product.Id),
            _ => products
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
        };
    }

    private async Task<ServiceResult<string>> GetCategoryNameAsync(
        int categoryId,
        CancellationToken cancellationToken)
    {
        var categoryName = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == categoryId)
            .Select(category => category.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return categoryName is null
            ? ServiceResult<string>.Failure(CreateError(
                ServiceErrorCategory.NotFound,
                ProductServiceErrorCodes.CategoryNotFound,
                "Kategori bulunamadı."))
            : ServiceResult<string>.Success(categoryName);
    }

    private Task<bool> SkuExistsAsync(
        string sku,
        int? excludedProductId,
        CancellationToken cancellationToken)
    {
        return dbContext.Products.AnyAsync(
            product => product.Sku == sku
                && (!excludedProductId.HasValue || product.Id != excludedProductId.Value),
            cancellationToken);
    }

    private async Task PersistChangesAsync(
        string operation,
        Product product,
        CancellationToken cancellationToken)
    {
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
            logger.LogError(
                exception,
                "Product persistence operation {Operation} failed for product {ProductId}.",
                operation,
                product.Id);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private static NormalizedProductListQuery NormalizeQuery(ProductListQueryModel? query)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query?.SearchTerm)
            ? null
            : query.SearchTerm.Trim();
        var categoryId = query?.CategoryId > 0 ? query.CategoryId : null;
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : ProductSortOrder.NameAscending;
        var page = query?.Page > 0 ? query.Page : 1;
        var pageSize = query?.PageSize > 0
            ? Math.Min(query.PageSize, MaximumPageSize)
            : DefaultPageSize;

        return new NormalizedProductListQuery(
            searchTerm,
            categoryId,
            query?.LowStockOnly ?? false,
            sortOrder,
            page,
            pageSize);
    }

    private static ServiceError? ValidateAndNormalizeInput(
        string? name,
        string? sku,
        decimal price,
        int minimumStockQuantity,
        int categoryId,
        out ValidatedProductInput validatedInput)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedSku = sku?.Trim() ?? string.Empty;
        validatedInput = new ValidatedProductInput(
            normalizedName,
            normalizedSku,
            price,
            minimumStockQuantity,
            categoryId);

        if (normalizedName.Length == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.NameRequired,
                "Ürün adı zorunludur.");
        }

        if (normalizedName.Length > MaximumNameLength)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.NameTooLong,
                $"Ürün adı en fazla {MaximumNameLength} karakter olabilir.");
        }

        if (normalizedSku.Length == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.SkuRequired,
                "SKU zorunludur.");
        }

        if (normalizedSku.Length > MaximumSkuLength)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.SkuTooLong,
                $"SKU en fazla {MaximumSkuLength} karakter olabilir.");
        }

        if (price <= 0
            || price > MaximumDatabaseAmount
            || decimal.Round(price, 2) != price)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.PriceInvalid,
                "Fiyat pozitif, en fazla iki ondalık basamaklı ve desteklenen tutar aralığında olmalıdır.");
        }

        if (minimumStockQuantity < 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.MinimumStockQuantityInvalid,
                "Minimum stok miktarı sıfır veya pozitif olmalıdır.");
        }

        if (categoryId <= 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                ProductServiceErrorCodes.CategoryInvalid,
                "Geçerli bir kategori seçilmelidir.");
        }

        return null;
    }

    private ServiceResult<T> Rejected<T>(
        string operation,
        ServiceError error,
        int productId = 0)
    {
        logger.LogWarning(
            "Product operation {Operation} rejected with error code {ErrorCode} for product {ProductId}.",
            operation,
            error.Code,
            productId);
        return ServiceResult<T>.Failure(error);
    }

    private static ProductViewModel ToViewModel(Product product, string categoryName)
    {
        return new ProductViewModel(
            product.Id,
            product.Name,
            product.Sku,
            product.Price,
            product.StockQuantity,
            product.MinimumStockQuantity,
            product.CategoryId,
            categoryName,
            product.StockQuantity <= product.MinimumStockQuantity);
    }

    private static ServiceError DuplicateSkuError()
    {
        return CreateError(
            ServiceErrorCategory.Validation,
            ProductServiceErrorCodes.SkuDuplicate,
            "Bu SKU başka bir ürün tarafından kullanılıyor.");
    }

    private static ServiceResult<T> ProductNotFound<T>()
    {
        return ServiceResult<T>.Failure(ProductNotFoundError());
    }

    private static ServiceError ProductNotFoundError()
    {
        return CreateError(
            ServiceErrorCategory.NotFound,
            ProductServiceErrorCodes.ProductNotFound,
            "Ürün bulunamadı.");
    }

    private static ServiceError CreateError(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return new ServiceError(category, code, message);
    }

    private sealed record NormalizedProductListQuery(
        string? SearchTerm,
        int? CategoryId,
        bool LowStockOnly,
        ProductSortOrder SortOrder,
        int Page,
        int PageSize);

    private sealed record ValidatedProductInput(
        string Name,
        string Sku,
        decimal Price,
        int MinimumStockQuantity,
        int CategoryId);
}
