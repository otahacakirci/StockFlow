using StockFlow.Entities;

namespace StockFlow.Services.Orders;

/// <summary>
/// Başarılı sipariş mutasyonundan sonra sunucu kimliğini, numarasını, nihai durumu ve hesaplanan toplamı taşır.
/// </summary>
public sealed record OrderMutationResult(
    int OrderId,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount);
