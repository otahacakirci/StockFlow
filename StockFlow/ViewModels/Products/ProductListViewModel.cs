namespace StockFlow.ViewModels.Products;

public sealed record ProductListViewModel(
    IReadOnlyList<ProductViewModel> Items,
    string? SearchTerm,
    int? CategoryId,
    bool LowStockOnly,
    ProductSortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
