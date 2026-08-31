namespace StockFlow.ViewModels.Suppliers;

public sealed class SupplierListQueryModel
{
    public string? SearchTerm { get; set; }

    public SupplierSortOrder SortOrder { get; set; } = SupplierSortOrder.CompanyNameAscending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
