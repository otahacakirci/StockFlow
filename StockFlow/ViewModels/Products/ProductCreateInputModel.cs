using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StockFlow.ModelBinding;

namespace StockFlow.ViewModels.Products;

public sealed class ProductCreateInputModel
{
    [Display(Name = "Ürün adı")]
    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [StringLength(150, ErrorMessage = "Ürün adı en fazla 150 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "SKU")]
    [Required(ErrorMessage = "SKU zorunludur.")]
    [StringLength(64, ErrorMessage = "SKU en fazla 64 karakter olabilir.")]
    public string Sku { get; set; } = string.Empty;

    [Display(Name = "Fiyat")]
    [ModelBinder(BinderType = typeof(TurkishDecimalModelBinder))]
    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "Fiyat sıfırdan büyük ve desteklenen tutar aralığında olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Display(Name = "Başlangıç stok miktarı")]
    [Range(0, int.MaxValue, ErrorMessage = "Başlangıç stok miktarı sıfır veya pozitif olmalıdır.")]
    public int StockQuantity { get; set; }

    [Display(Name = "Minimum stok miktarı")]
    [Range(0, int.MaxValue, ErrorMessage = "Minimum stok miktarı sıfır veya pozitif olmalıdır.")]
    public int MinimumStockQuantity { get; set; }

    [Display(Name = "Kategori")]
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçilmelidir.")]
    public int CategoryId { get; set; }
}
