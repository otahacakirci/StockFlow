using StockFlow.Services.Customers;
using StockFlow.Services.Products;
using StockFlow.Services.Suppliers;

namespace StockFlow.Services.Orders;

/// <summary>
/// Sipariş Service işlemlerinin beklenen hataları için istemcilerce güvenle eşleştirilebilen kararlı kodları toplar.
/// </summary>
public static class OrderServiceErrorCodes
{
    public const string InputRequired = "order.input_required";
    public const string CreatorRequired = "order.creator_required";
    public const string UserNotFound = "user.not_found";
    public const string InvalidOrderType = "order.type_invalid";
    public const string InvalidParty = "order.party_invalid";
    // Kaynak uyumluluğu için korunur; taraf hata kodlarının sahibi ilgili domain servisidir.
    public const string CustomerNotFound = CustomerServiceErrorCodes.CustomerNotFound;
    public const string SupplierNotFound = SupplierServiceErrorCodes.SupplierNotFound;
    public const string ItemsRequired = "order.items_required";
    public const string InvalidProduct = "order.item.product_invalid";
    public const string InvalidQuantity = "order.item.quantity_invalid";
    public const string DuplicateProduct = "order.item.duplicate_product";
    // Kaynak uyumluluğu için korunur; Product hata kodlarının sahibi Product domain servisidir.
    public const string ProductNotFound = ProductServiceErrorCodes.ProductNotFound;
    public const string InvalidProductPrice = ProductServiceErrorCodes.PriceInvalid;
    public const string TotalOutOfRange = "order.total_out_of_range";
    public const string OrderNotFound = "order.not_found";
    public const string OrderNotDraft = "order.not_draft";
    public const string InsufficientStock = "stock.insufficient";
    public const string StockQuantityOutOfRange = "stock.quantity_out_of_range";
    public const string DraftHasStockMovements = "order.draft_has_stock_movements";
}
