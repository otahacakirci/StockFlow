using System.ComponentModel.DataAnnotations;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Tests.ViewModels.Customers;

public sealed class CustomerInputModelTests
{
    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsTurkishRequiredMessage()
    {
        var errors = Validate(new CustomerInputModel { Name = string.Empty });

        var error = Assert.Single(errors);
        Assert.Equal("Müşteri adı zorunludur.", error.ErrorMessage);
        Assert.Equal([nameof(CustomerInputModel.Name)], error.MemberNames);
    }

    [Fact]
    public void Validate_WhenOptionalContactFieldsAreEmpty_ReturnsNoErrors()
    {
        var errors = Validate(new CustomerInputModel
        {
            Name = "Valid Customer",
            Email = null,
            Phone = null,
            Address = null
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenEmailOrPhoneFormatIsInvalid_ReturnsTurkishMessages()
    {
        var errors = Validate(new CustomerInputModel
        {
            Name = "Valid Customer",
            Email = "invalid-email",
            Phone = "not-a-phone"
        });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Geçerli bir e-posta adresi girilmelidir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Email)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Geçerli bir telefon numarası girilmelidir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Phone)]));
    }

    [Fact]
    public void Validate_WhenFieldsExceedMaximums_ReturnsTurkishLengthMessages()
    {
        var errors = Validate(new CustomerInputModel
        {
            Name = new string('n', 151),
            Email = new string('e', 257),
            Phone = new string('1', 33),
            Address = new string('a', 501)
        });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Müşteri adı en fazla 150 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Name)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "E-posta adresi en fazla 256 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Email)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Telefon numarası en fazla 32 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Phone)]));
        Assert.Contains(errors, error =>
            error.ErrorMessage == "Adres en fazla 500 karakter olabilir."
            && error.MemberNames.SequenceEqual([nameof(CustomerInputModel.Address)]));
    }

    [Fact]
    public void Validate_WhenFieldsAreWithinLimits_ReturnsNoErrors()
    {
        var errors = Validate(new CustomerInputModel
        {
            Name = new string('n', 150),
            Email = "valid.customer@example.com",
            Phone = "+90 555 123 4567",
            Address = new string('a', 500)
        });

        Assert.Empty(errors);
    }

    private static IReadOnlyList<ValidationResult> Validate(CustomerInputModel model)
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
