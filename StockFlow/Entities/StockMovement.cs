namespace StockFlow.Entities;

public class StockMovement
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public StockMovementType Type { get; set; }

    public int Quantity { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime MovementDate { get; set; }
}
