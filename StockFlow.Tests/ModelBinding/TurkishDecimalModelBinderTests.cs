using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using StockFlow.ModelBinding;

namespace StockFlow.Tests.ModelBinding;

public sealed class TurkishDecimalModelBinderTests
{
    [Theory]
    [InlineData("19,34", "19.34")]
    [InlineData("19,3", "19.3")]
    [InlineData("19", "19")]
    [InlineData(" 19,34 ", "19.34")]
    public async Task BindModelAsync_AcceptsPlainTurkishDecimalValues(
        string rawValue,
        string expectedInvariantValue)
    {
        var bindingContext = CreateBindingContext(rawValue);

        await new TurkishDecimalModelBinder().BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(
            decimal.Parse(expectedInvariantValue, CultureInfo.InvariantCulture),
            Assert.IsType<decimal>(bindingContext.Result.Model));
        var modelState = bindingContext.ModelState[ModelName]!;
        Assert.Empty(modelState.Errors);
        Assert.Equal(rawValue, modelState.AttemptedValue);
    }

    [Theory]
    [InlineData("19.34")]
    [InlineData("1.234,56")]
    [InlineData("19,345")]
    [InlineData("12 345,67")]
    [InlineData("999999999999999999999999999999999999,99")]
    public async Task BindModelAsync_RejectsAmbiguousOrUnsupportedValues(string rawValue)
    {
        var bindingContext = CreateBindingContext(rawValue);

        await new TurkishDecimalModelBinder().BindModelAsync(bindingContext);

        Assert.False(bindingContext.Result.IsModelSet);
        Assert.Equal(
            TurkishDecimalModelBinder.InvalidFormatErrorMessage,
            Assert.Single(bindingContext.ModelState[ModelName]!.Errors).ErrorMessage);
        Assert.Equal(rawValue, bindingContext.ModelState[ModelName]!.AttemptedValue);
    }

    [Fact]
    public async Task BindModelAsync_RejectsEmptyValue()
    {
        var bindingContext = CreateBindingContext(string.Empty);

        await new TurkishDecimalModelBinder().BindModelAsync(bindingContext);

        Assert.False(bindingContext.Result.IsModelSet);
        Assert.Equal(
            TurkishDecimalModelBinder.EmptyValueErrorMessage,
            Assert.Single(bindingContext.ModelState[ModelName]!.Errors).ErrorMessage);
    }

    private const string ModelName = "Input.Price";

    private static ModelBindingContext CreateBindingContext(string rawValue)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal));
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            [ModelName] = rawValue
        });
        var valueProvider = new QueryStringValueProvider(
            BindingSource.Query,
            query,
            CultureInfo.InvariantCulture);

        return DefaultModelBindingContext.CreateBindingContext(
            actionContext,
            valueProvider,
            metadata,
            bindingInfo: null,
            ModelName);
    }
}
