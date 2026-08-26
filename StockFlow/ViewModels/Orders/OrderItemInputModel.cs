using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Orders;

public sealed class OrderItemInputModel
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
