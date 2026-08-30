using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Categories;

public sealed class CategoryInputModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
