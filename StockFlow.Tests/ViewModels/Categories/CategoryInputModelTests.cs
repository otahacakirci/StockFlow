using System.ComponentModel.DataAnnotations;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Tests.ViewModels.Categories;

public sealed class CategoryInputModelTests
{
    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsTurkishRequiredMessage()
    {
        var errors = Validate(new CategoryInputModel { Name = string.Empty });

        var error = Assert.Single(errors);
        Assert.Equal("Kategori adı zorunludur.", error.ErrorMessage);
        Assert.Equal([nameof(CategoryInputModel.Name)], error.MemberNames);
    }

    [Fact]
    public void Validate_WhenNameExceedsMaximum_ReturnsTurkishLengthMessage()
    {
        var errors = Validate(new CategoryInputModel { Name = new string('x', 101) });

        var error = Assert.Single(errors);
        Assert.Equal("Kategori adı en fazla 100 karakter olabilir.", error.ErrorMessage);
        Assert.Equal([nameof(CategoryInputModel.Name)], error.MemberNames);
    }

    [Fact]
    public void Validate_WhenNameIsAtMaximumLength_ReturnsNoErrors()
    {
        var errors = Validate(new CategoryInputModel { Name = new string('x', 100) });

        Assert.Empty(errors);
    }

    private static IReadOnlyList<ValidationResult> Validate(CategoryInputModel model)
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
