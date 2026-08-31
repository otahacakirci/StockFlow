namespace StockFlow.ViewModels.Suppliers;

public sealed record SupplierListViewModel(
    IReadOnlyList<SupplierViewModel> Items,
    string? SearchTerm,
    SupplierSortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
