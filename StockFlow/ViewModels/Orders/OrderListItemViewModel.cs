using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed record OrderListItemViewModel(
    int Id,
    string OrderNumber,
    OrderType Type,
    OrderStatus Status,
    DateTime OrderDate,
    decimal TotalAmount,
    string PartyName,
    int ItemCount);
