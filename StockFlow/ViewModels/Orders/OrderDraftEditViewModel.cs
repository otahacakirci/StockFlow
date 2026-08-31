using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed record OrderDraftEditViewModel(
    int Id,
    string OrderNumber,
    DateTime OrderDate,
    OrderType Type,
    int? CustomerId,
    int? SupplierId,
    decimal TotalAmount,
    IReadOnlyList<OrderDraftEditItemViewModel> Items);
