using StockFlow.Entities;

namespace StockFlow.Services.Orders;

public sealed record OrderMutationResult(
    int OrderId,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount);
