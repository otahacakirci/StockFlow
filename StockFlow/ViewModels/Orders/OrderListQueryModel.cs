using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed class OrderListQueryModel
{
    public OrderType? Type { get; set; }

    public OrderStatus? Status { get; set; }

    public OrderSortOrder SortOrder { get; set; } = OrderSortOrder.DateDescending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
