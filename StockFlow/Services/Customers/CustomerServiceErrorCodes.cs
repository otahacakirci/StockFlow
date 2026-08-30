namespace StockFlow.Services.Customers;

/// <summary>
/// Customer Service işlemlerinin beklenen hataları için güvenle eşleştirilebilen kararlı kodları toplar.
/// </summary>
public static class CustomerServiceErrorCodes
{
    public const string InputRequired = "customer.input_required";
    public const string NameRequired = "customer.name_required";
    public const string NameTooLong = "customer.name_too_long";
    public const string EmailTooLong = "customer.email_too_long";
    public const string EmailInvalid = "customer.email_invalid";
    public const string PhoneTooLong = "customer.phone_too_long";
    public const string PhoneInvalid = "customer.phone_invalid";
    public const string AddressTooLong = "customer.address_too_long";
    public const string CustomerNotFound = "customer.not_found";
    public const string CustomerHasOrders = "customer.has_orders";
}
