namespace StockFlow.ViewModels.Customers;

public sealed record CustomerListViewModel(
    IReadOnlyList<CustomerViewModel> Items,
    string? SearchTerm,
    CustomerSortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
