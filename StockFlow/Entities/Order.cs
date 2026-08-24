namespace StockFlow.Entities;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderType Type { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public string? CreatedByUserId { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
