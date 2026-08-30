namespace StockFlow.ViewModels.Customers;

public sealed record CustomerViewModel(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    int OrderCount);
