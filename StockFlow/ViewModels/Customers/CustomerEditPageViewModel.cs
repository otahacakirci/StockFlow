namespace StockFlow.ViewModels.Customers;

public sealed record CustomerEditPageViewModel(
    int Id,
    CustomerInputModel Input,
    string ReturnUrl);
