using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Orders;

public sealed class OrderItemInputModel
{
    [Display(Name = "Ürün")]
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir ürün seçilmelidir.")]
    public int ProductId { get; set; }

    [Display(Name = "Miktar")]
    [Range(1, int.MaxValue, ErrorMessage = "Miktar sıfırdan büyük olmalıdır.")]
    public int Quantity { get; set; }
}
