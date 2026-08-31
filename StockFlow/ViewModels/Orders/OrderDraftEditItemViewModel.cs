namespace StockFlow.ViewModels.Orders;

public sealed record OrderDraftEditItemViewModel(
    int ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    decimal UnitPrice);
