using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StockFlow.ModelBinding;

public sealed class TurkishDecimalModelBinder : IModelBinder
{
    public const string EmptyValueErrorMessage = "Fiyat alanı zorunludur.";
    public const string InvalidFormatErrorMessage =
        "Fiyatı 19,34 biçiminde, virgülden sonra en fazla iki basamakla girin.";

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Regex DecimalPattern = new(
        @"^-?\d+(?:,\d{1,2})?$",
        RegexOptions.CultureInvariant);

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        var rawValue = valueResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(rawValue))
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                EmptyValueErrorMessage);
            return Task.CompletedTask;
        }

        const NumberStyles allowedStyles =
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!DecimalPattern.IsMatch(rawValue)
            || !decimal.TryParse(rawValue, allowedStyles, TurkishCulture, out var value))
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                InvalidFormatErrorMessage);
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(value);
        return Task.CompletedTask;
    }
}
