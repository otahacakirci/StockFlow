namespace StockFlow.ViewModels.Customers;

public sealed class CustomerListQueryModel
{
    public string? SearchTerm { get; set; }

    public CustomerSortOrder SortOrder { get; set; } = CustomerSortOrder.NameAscending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
