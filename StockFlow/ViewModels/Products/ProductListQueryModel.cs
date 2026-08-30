namespace StockFlow.ViewModels.Products;

public sealed class ProductListQueryModel
{
    public string? SearchTerm { get; set; }

    public int? CategoryId { get; set; }

    public bool LowStockOnly { get; set; }

    public ProductSortOrder SortOrder { get; set; } = ProductSortOrder.NameAscending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
