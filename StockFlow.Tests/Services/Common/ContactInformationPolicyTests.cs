using StockFlow.Services.Common;

namespace StockFlow.Tests.Services.Common;

public sealed class ContactInformationPolicyTests
{
    private static readonly ContactInformationErrorCodes ErrorCodes = new(
        "test.email_too_long",
        "test.email_invalid",
        "test.phone_too_long",
        "test.phone_invalid",
        "test.address_too_long");

    [Fact]
    public void ValidateAndNormalize_TrimsValuesAndConvertsWhitespaceToNull()
    {
        var result = ContactInformationPolicy.ValidateAndNormalize(
            "  contact@example.com  ",
            "   ",
            $"  {new string('a', 500)}  ",
            ErrorCodes);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var contact = Assert.IsType<NormalizedContactInformation>(result.Value);
        Assert.Equal("contact@example.com", contact.Email);
        Assert.Null(contact.Phone);
        Assert.Equal(new string('a', 500), contact.Address);
    }

    [Fact]
    public void ValidateAndNormalize_ReturnsConfiguredCodesAndSafeMessages()
    {
        AssertFailure(
            ContactInformationPolicy.ValidateAndNormalize(
                new string('e', 257),
                null,
                null,
                ErrorCodes),
            ErrorCodes.EmailTooLong,
            "E-posta adresi en fazla 256 karakter olabilir.");
        AssertFailure(
            ContactInformationPolicy.ValidateAndNormalize(
                "invalid-email",
                null,
                null,
                ErrorCodes),
            ErrorCodes.EmailInvalid,
            "Geçerli bir e-posta adresi girilmelidir.");
        AssertFailure(
            ContactInformationPolicy.ValidateAndNormalize(
                null,
                new string('1', 33),
                null,
                ErrorCodes),
            ErrorCodes.PhoneTooLong,
            "Telefon numarası en fazla 32 karakter olabilir.");
        AssertFailure(
            ContactInformationPolicy.ValidateAndNormalize(
                null,
                "not-a-phone",
                null,
                ErrorCodes),
            ErrorCodes.PhoneInvalid,
            "Geçerli bir telefon numarası girilmelidir.");
        AssertFailure(
            ContactInformationPolicy.ValidateAndNormalize(
                null,
                null,
                new string('a', 501),
                ErrorCodes),
            ErrorCodes.AddressTooLong,
            "Adres en fazla 500 karakter olabilir.");
    }

    private static void AssertFailure(
        ServiceResult<NormalizedContactInformation> result,
        string code,
        string message)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ServiceErrorCategory.Validation, result.Error.Category);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(message, result.Error.Message);
    }
}
