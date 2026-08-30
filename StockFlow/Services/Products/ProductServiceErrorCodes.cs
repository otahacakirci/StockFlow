namespace StockFlow.Services.Products;

/// <summary>
/// Product Service işlemlerinin beklenen hataları için güvenle eşleştirilebilen kararlı kodları toplar.
/// </summary>
public static class ProductServiceErrorCodes
{
    public const string InputRequired = "product.input_required";
    public const string NameRequired = "product.name_required";
    public const string NameTooLong = "product.name_too_long";
    public const string SkuRequired = "product.sku_required";
    public const string SkuTooLong = "product.sku_too_long";
    public const string SkuDuplicate = "product.sku_duplicate";
    public const string PriceInvalid = "product.price_invalid";
    public const string StockQuantityInvalid = "product.stock_quantity_invalid";
    public const string MinimumStockQuantityInvalid = "product.minimum_stock_quantity_invalid";
    public const string CategoryInvalid = "product.category_invalid";
    public const string CategoryNotFound = "category.not_found";
    public const string ProductNotFound = "product.not_found";
    public const string ProductHasHistory = "product.has_history";
}
