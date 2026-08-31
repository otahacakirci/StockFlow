using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Suppliers;

public sealed class SupplierInputModel
{
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(32)]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}
