using System.ComponentModel.DataAnnotations;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Tests.ViewModels.Suppliers;

public sealed class SupplierInputModelTests
{
    [Fact]
    public void Validate_WhenCompanyNameIsEmpty_ReturnsTurkishRequiredMessage()
    {
        var errors = Validate(new SupplierInputModel { CompanyName = string.Empty });

        var error = Assert.Single(errors);
        Assert.Equal("Şirket adı zorunludur.", error.ErrorMessage);
        Assert.Equal([nameof(SupplierInputModel.CompanyName)], error.MemberNames);
    }

    [Fact]
    public void Validate_WhenOptionalContactFieldsAreNull_ReturnsNoErrors()
    {
        var errors = Validate(new SupplierInputModel
        {
            CompanyName = "Valid Supplier",
            Email = null,
            Phone = null,
            Address = null
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenEmailOrPhoneFormatIsInvalid_ReturnsTurkishMessages()
    {
        var errors = Validate(new SupplierInputModel
        {
            CompanyName = "Valid Supplier",
            Email = "invalid-email",
            Phone = "not-a-phone"
        });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Geçerli bir e-posta adresi girilmelidir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.Email)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Geçerli bir telefon numarası girilmelidir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.Phone)]));
    }

    [Fact]
    public void Validate_WhenFieldsExceedMaximums_ReturnsTurkishLengthMessages()
    {
        var errors = Validate(new SupplierInputModel
        {
            CompanyName = new string('n', 201),
            Email = new string('e', 257),
            Phone = new string('1', 33),
            Address = new string('a', 501)
        });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Şirket adı en fazla 200 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.CompanyName)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "E-posta adresi en fazla 256 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.Email)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Telefon numarası en fazla 32 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.Phone)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Adres en fazla 500 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(SupplierInputModel.Address)]));
    }

    [Fact]
    public void Validate_WhenFieldsAreWithinLimits_ReturnsNoErrors()
    {
        var errors = Validate(new SupplierInputModel
        {
            CompanyName = new string('n', 200),
            Email = "valid.supplier@example.com",
            Phone = "+90 555 123 4567",
            Address = new string('a', 500)
        });

        Assert.Empty(errors);
    }

    private static IReadOnlyList<ValidationResult> Validate(SupplierInputModel model)
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
