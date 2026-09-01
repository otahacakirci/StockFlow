using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Categories;

public sealed class CategoryInputModel
{
    [Display(Name = "Kategori adı")]
    [Required(ErrorMessage = "Kategori adı zorunludur.")]
    [StringLength(100, ErrorMessage = "Kategori adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;
}
