using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Customers;

public sealed class CustomerInputModel
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(32)]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}
