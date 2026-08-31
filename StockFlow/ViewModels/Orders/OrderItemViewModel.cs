namespace StockFlow.ViewModels.Orders;

public sealed record OrderItemViewModel(
    int Id,
    int ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
