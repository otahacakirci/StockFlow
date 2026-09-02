(function ($) {
    "use strict";

    if (!$ || !$.validator) {
        return;
    }

    const turkishDecimalSelector = "[data-stockflow-turkish-decimal='true']";
    const turkishDecimalPattern = /^-?\d+(?:,\d{1,2})?$/;
    const originalNumber = $.validator.methods.number;
    const originalRange = $.validator.methods.range;

    function usesTurkishDecimal(element) {
        return element.matches(turkishDecimalSelector);
    }

    function parseTurkishDecimal(value) {
        return Number(value.replace(",", "."));
    }

    $.validator.methods.number = function (value, element) {
        if (!usesTurkishDecimal(element)) {
            return originalNumber.call(this, value, element);
        }

        return this.optional(element) || turkishDecimalPattern.test(value.trim());
    };

    $.validator.methods.range = function (value, element, parameters) {
        if (!usesTurkishDecimal(element)) {
            return originalRange.call(this, value, element, parameters);
        }

        if (this.optional(element)) {
            return true;
        }

        const normalizedValue = value.trim();
        if (!turkishDecimalPattern.test(normalizedValue)) {
            return false;
        }

        const numericValue = parseTurkishDecimal(normalizedValue);
        const minimum = Number(parameters[0]);
        const maximum = Number(parameters[1]);

        return Number.isFinite(numericValue)
            && numericValue >= minimum
            && numericValue <= maximum;
    };
}(window.jQuery));
