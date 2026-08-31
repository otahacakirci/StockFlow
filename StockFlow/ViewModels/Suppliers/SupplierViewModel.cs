namespace StockFlow.ViewModels.Suppliers;

public sealed record SupplierViewModel(
    int Id,
    string CompanyName,
    string? Email,
    string? Phone,
    string? Address,
    int OrderCount);
