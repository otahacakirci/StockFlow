using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed record OrderDetailViewModel(
    int Id,
    string OrderNumber,
    OrderType Type,
    OrderStatus Status,
    DateTime OrderDate,
    decimal TotalAmount,
    int? CustomerId,
    string? CustomerName,
    int? SupplierId,
    string? SupplierCompanyName,
    IReadOnlyList<OrderItemViewModel> Items);
