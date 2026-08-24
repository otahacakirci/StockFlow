using Microsoft.AspNetCore.Identity;

namespace StockFlow.Entities;

public class ApplicationUser : IdentityUser
{
    public ICollection<Order> CreatedOrders { get; set; } = new List<Order>();
}
