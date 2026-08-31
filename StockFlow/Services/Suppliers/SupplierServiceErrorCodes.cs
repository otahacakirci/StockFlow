namespace StockFlow.Services.Suppliers;

/// <summary>
/// Supplier Service işlemlerinin beklenen hataları için güvenle eşleştirilebilen kararlı kodları toplar.
/// </summary>
public static class SupplierServiceErrorCodes
{
    public const string InputRequired = "supplier.input_required";
    public const string CompanyNameRequired = "supplier.company_name_required";
    public const string CompanyNameTooLong = "supplier.company_name_too_long";
    public const string EmailTooLong = "supplier.email_too_long";
    public const string EmailInvalid = "supplier.email_invalid";
    public const string PhoneTooLong = "supplier.phone_too_long";
    public const string PhoneInvalid = "supplier.phone_invalid";
    public const string AddressTooLong = "supplier.address_too_long";
    public const string SupplierNotFound = "supplier.not_found";
    public const string SupplierHasOrders = "supplier.has_orders";
}
