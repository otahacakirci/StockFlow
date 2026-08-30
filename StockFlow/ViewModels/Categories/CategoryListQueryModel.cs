namespace StockFlow.ViewModels.Categories;

public sealed class CategoryListQueryModel
{
    public string? SearchTerm { get; set; }

    public CategorySortOrder SortOrder { get; set; } = CategorySortOrder.NameAscending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
