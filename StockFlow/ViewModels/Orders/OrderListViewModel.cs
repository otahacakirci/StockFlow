using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed record OrderListViewModel(
    IReadOnlyList<OrderListItemViewModel> Items,
    OrderType? Type,
    OrderStatus? Status,
    OrderSortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
