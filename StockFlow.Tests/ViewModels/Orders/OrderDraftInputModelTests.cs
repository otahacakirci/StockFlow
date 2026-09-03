using System.ComponentModel.DataAnnotations;
using StockFlow.Entities;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Tests.ViewModels.Orders;

public sealed class OrderDraftInputModelTests
{
    [Fact]
    public void DraftPostContract_DoesNotExposeClientPriceOrTotal()
    {
        Assert.Null(typeof(OrderDraftInputModel).GetProperty("TotalAmount"));
        Assert.Null(typeof(OrderItemInputModel).GetProperty("UnitPrice"));
        Assert.Equal(
            [nameof(OrderItemInputModel.ProductId), nameof(OrderItemInputModel.Quantity)],
            typeof(OrderItemInputModel).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void SaleAndPurchaseInputs_RequireOnlyTheirMatchingParty()
    {
        var sale = ValidInput(OrderType.Sale, customerId: 4, supplierId: null);
        var purchase = ValidInput(OrderType.Purchase, customerId: null, supplierId: 5);

        Assert.Empty(Validate(sale));
        Assert.Empty(Validate(purchase));

        var invalidSale = ValidInput(OrderType.Sale, customerId: null, supplierId: 5);
        var invalidPurchase = ValidInput(OrderType.Purchase, customerId: 4, supplierId: null);

        Assert.Contains(
            Validate(invalidSale),
            error => error.MemberNames.Contains(nameof(OrderDraftInputModel.CustomerId)));
        Assert.Contains(
            Validate(invalidSale),
            error => error.MemberNames.Contains(nameof(OrderDraftInputModel.SupplierId)));
        Assert.Contains(
            Validate(invalidPurchase),
            error => error.MemberNames.Contains(nameof(OrderDraftInputModel.CustomerId)));
        Assert.Contains(
            Validate(invalidPurchase),
            error => error.MemberNames.Contains(nameof(OrderDraftInputModel.SupplierId)));
    }

    [Fact]
    public void DraftInput_RejectsEmptyAndDuplicateItemsWithTurkishMessages()
    {
        var empty = ValidInput(OrderType.Sale, customerId: 4, supplierId: null);
        empty.Items = [];
        var duplicate = ValidInput(OrderType.Sale, customerId: 4, supplierId: null);
        duplicate.Items.Add(new OrderItemInputModel { ProductId = 7, Quantity = 2 });

        Assert.Contains(
            Validate(empty),
            error => error.ErrorMessage == "Sipariş en az bir kalem içermelidir.");
        Assert.Contains(
            Validate(duplicate),
            error => error.ErrorMessage == "Aynı ürün siparişte birden fazla kalem olarak yer alamaz.");
    }

    [Fact]
    public void OrderItem_RequiresProductAndPositiveQuantityWithTurkishMessages()
    {
        var errors = Validate(new OrderItemInputModel { ProductId = 0, Quantity = 0 });

        Assert.Contains(
            errors,
            error => error.ErrorMessage == "Geçerli bir ürün seçilmelidir.");
        Assert.Contains(
            errors,
            error => error.ErrorMessage == "Miktar sıfırdan büyük olmalıdır.");
    }

    private static OrderDraftInputModel ValidInput(
        OrderType type,
        int? customerId,
        int? supplierId)
    {
        return new OrderDraftInputModel
        {
            Type = type,
            CustomerId = customerId,
            SupplierId = supplierId,
            Items = [new OrderItemInputModel { ProductId = 7, Quantity = 1 }]
        };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
