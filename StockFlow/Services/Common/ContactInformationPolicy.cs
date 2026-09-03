using System.ComponentModel.DataAnnotations;

namespace StockFlow.Services.Common;

/// <summary>
/// Customer ve Supplier iletişim alanlarının ortak normalizasyon ve doğrulama politikasını uygular.
/// </summary>
internal static class ContactInformationPolicy
{
    private const int MaximumEmailLength = 256;
    private const int MaximumPhoneLength = 32;
    private const int MaximumAddressLength = 500;
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly PhoneAttribute PhoneValidator = new();

    internal static ServiceResult<NormalizedContactInformation> ValidateAndNormalize(
        string? email,
        string? phone,
        string? address,
        ContactInformationErrorCodes errorCodes)
    {
        ArgumentNullException.ThrowIfNull(errorCodes);

        var normalizedContact = new NormalizedContactInformation(
            NormalizeOptional(email),
            NormalizeOptional(phone),
            NormalizeOptional(address));

        if (normalizedContact.Email?.Length > MaximumEmailLength)
        {
            return Failure(
                errorCodes.EmailTooLong,
                $"E-posta adresi en fazla {MaximumEmailLength} karakter olabilir.");
        }

        if (normalizedContact.Email is not null
            && !EmailValidator.IsValid(normalizedContact.Email))
        {
            return Failure(
                errorCodes.EmailInvalid,
                "Geçerli bir e-posta adresi girilmelidir.");
        }

        if (normalizedContact.Phone?.Length > MaximumPhoneLength)
        {
            return Failure(
                errorCodes.PhoneTooLong,
                $"Telefon numarası en fazla {MaximumPhoneLength} karakter olabilir.");
        }

        if (normalizedContact.Phone is not null
            && !PhoneValidator.IsValid(normalizedContact.Phone))
        {
            return Failure(
                errorCodes.PhoneInvalid,
                "Geçerli bir telefon numarası girilmelidir.");
        }

        if (normalizedContact.Address?.Length > MaximumAddressLength)
        {
            return Failure(
                errorCodes.AddressTooLong,
                $"Adres en fazla {MaximumAddressLength} karakter olabilir.");
        }

        return ServiceResult<NormalizedContactInformation>.Success(normalizedContact);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ServiceResult<NormalizedContactInformation> Failure(
        string code,
        string message)
    {
        return ServiceResult<NormalizedContactInformation>.Failure(new ServiceError(
            ServiceErrorCategory.Validation,
            code,
            message));
    }
}

internal sealed record ContactInformationErrorCodes(
    string EmailTooLong,
    string EmailInvalid,
    string PhoneTooLong,
    string PhoneInvalid,
    string AddressTooLong);

internal sealed record NormalizedContactInformation(
    string? Email,
    string? Phone,
    string? Address);
