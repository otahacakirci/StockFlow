namespace StockFlow.Entities;

public class Supplier
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
