using System.ComponentModel.DataAnnotations;
using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed class OrderDraftInputModel
{
    [EnumDataType(typeof(OrderType))]
    public OrderType Type { get; set; }

    public int? CustomerId { get; set; }

    public int? SupplierId { get; set; }

    [MinLength(1)]
    public List<OrderItemInputModel> Items { get; set; } = [];
}
