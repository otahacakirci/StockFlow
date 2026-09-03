using System.ComponentModel.DataAnnotations;
using StockFlow.Entities;

namespace StockFlow.ViewModels.Orders;

public sealed class OrderDraftInputModel : IValidatableObject
{
    [Display(Name = "Sipariş türü")]
    [EnumDataType(typeof(OrderType), ErrorMessage = "Geçerli bir sipariş türü seçilmelidir.")]
    public OrderType Type { get; set; }

    [Display(Name = "Müşteri")]
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir müşteri seçilmelidir.")]
    public int? CustomerId { get; set; }

    [Display(Name = "Tedarikçi")]
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir tedarikçi seçilmelidir.")]
    public int? SupplierId { get; set; }

    [MinLength(1, ErrorMessage = "Sipariş en az bir kalem içermelidir.")]
    public List<OrderItemInputModel> Items { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield break;
        }

        if (Type == OrderType.Sale)
        {
            if (CustomerId is null or <= 0)
            {
                yield return new ValidationResult(
                    "Satış siparişi için geçerli bir müşteri seçilmelidir.",
                    [nameof(CustomerId)]);
            }

            if (SupplierId is not null)
            {
                yield return new ValidationResult(
                    "Satış siparişinde tedarikçi seçilmemelidir.",
                    [nameof(SupplierId)]);
            }
        }
        else
        {
            if (SupplierId is null or <= 0)
            {
                yield return new ValidationResult(
                    "Satın alma siparişi için geçerli bir tedarikçi seçilmelidir.",
                    [nameof(SupplierId)]);
            }

            if (CustomerId is not null)
            {
                yield return new ValidationResult(
                    "Satın alma siparişinde müşteri seçilmemelidir.",
                    [nameof(CustomerId)]);
            }
        }

        if (Items is { Count: > 0 }
            && Items.Where(item => item.ProductId > 0)
                .GroupBy(item => item.ProductId)
                .Any(group => group.Count() > 1))
        {
            yield return new ValidationResult(
                "Aynı ürün siparişte birden fazla kalem olarak yer alamaz.",
                [nameof(Items)]);
        }
    }
}
