namespace StockFlow.ViewModels.Suppliers;

public sealed record SupplierEditPageViewModel(
    int Id,
    SupplierInputModel Input,
    string ReturnUrl);
