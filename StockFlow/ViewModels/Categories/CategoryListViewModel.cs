namespace StockFlow.ViewModels.Categories;

public sealed record CategoryListViewModel(
    IReadOnlyList<CategoryViewModel> Items,
    string? SearchTerm,
    CategorySortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
