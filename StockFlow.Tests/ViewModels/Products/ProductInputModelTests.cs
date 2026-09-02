using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using StockFlow.ModelBinding;
using StockFlow.ViewModels.Products;

namespace StockFlow.Tests.ViewModels.Products;

public sealed class ProductInputModelTests
{
    [Fact]
    public void CreateAndUpdatePrices_UseStrictTurkishDecimalBinder()
    {
        AssertPriceBinder(typeof(ProductCreateInputModel));
        AssertPriceBinder(typeof(ProductUpdateInputModel));
    }

    [Fact]
    public void CreateInput_UsesTurkishRequiredAndLengthMessages()
    {
        var input = ValidCreate();
        input.Name = string.Empty;
        input.Sku = new string('s', 65);

        var errors = Validate(input);

        AssertValidationError(errors, nameof(input.Name), "Ürün adı zorunludur.");
        AssertValidationError(errors, nameof(input.Sku), "SKU en fazla 64 karakter olabilir.");

        input.Name = new string('n', 151);
        input.Sku = string.Empty;
        errors = Validate(input);

        AssertValidationError(errors, nameof(input.Name), "Ürün adı en fazla 150 karakter olabilir.");
        AssertValidationError(errors, nameof(input.Sku), "SKU zorunludur.");
    }

    [Fact]
    public void CreateInput_UsesTurkishNumericAndCategoryMessages()
    {
        var input = ValidCreate();
        input.Price = 0;
        input.StockQuantity = -1;
        input.MinimumStockQuantity = -1;
        input.CategoryId = 0;

        var errors = Validate(input);

        AssertValidationError(
            errors,
            nameof(input.Price),
            "Fiyat sıfırdan büyük ve desteklenen tutar aralığında olmalıdır.");
        AssertValidationError(
            errors,
            nameof(input.StockQuantity),
            "Başlangıç stok miktarı sıfır veya pozitif olmalıdır.");
        AssertValidationError(
            errors,
            nameof(input.MinimumStockQuantity),
            "Minimum stok miktarı sıfır veya pozitif olmalıdır.");
        AssertValidationError(
            errors,
            nameof(input.CategoryId),
            "Geçerli bir kategori seçilmelidir.");
    }

    [Fact]
    public void UpdateInput_HasNoStockQuantityAndValidatesEditableFields()
    {
        Assert.Null(typeof(ProductUpdateInputModel).GetProperty(nameof(ProductCreateInputModel.StockQuantity)));

        var input = new ProductUpdateInputModel
        {
            Name = "Valid Product",
            Sku = "VALID-SKU",
            Price = 1.00m,
            MinimumStockQuantity = -1,
            CategoryId = 0
        };

        var errors = Validate(input);

        AssertValidationError(
            errors,
            nameof(input.MinimumStockQuantity),
            "Minimum stok miktarı sıfır veya pozitif olmalıdır.");
        AssertValidationError(
            errors,
            nameof(input.CategoryId),
            "Geçerli bir kategori seçilmelidir.");
    }

    private static ProductCreateInputModel ValidCreate()
    {
        return new ProductCreateInputModel
        {
            Name = "Valid Product",
            Sku = "VALID-SKU",
            Price = 1.00m,
            StockQuantity = 0,
            MinimumStockQuantity = 0,
            CategoryId = 1
        };
    }

    private static void AssertPriceBinder(Type inputType)
    {
        var property = inputType.GetProperty(nameof(ProductCreateInputModel.Price));
        Assert.NotNull(property);
        var binder = Assert.Single(property.GetCustomAttributes<ModelBinderAttribute>());
        Assert.Equal(typeof(TurkishDecimalModelBinder), binder.BinderType);
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

    private static void AssertValidationError(
        IEnumerable<ValidationResult> errors,
        string memberName,
        string expectedMessage)
    {
        var error = Assert.Single(errors, candidate => candidate.MemberNames.Contains(memberName));
        Assert.Equal(expectedMessage, error.ErrorMessage);
    }
}
