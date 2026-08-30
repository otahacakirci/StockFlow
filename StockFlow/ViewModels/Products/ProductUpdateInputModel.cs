using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Products;

public sealed class ProductUpdateInputModel
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int MinimumStockQuantity { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}
