using StockFlow.Entities;

namespace StockFlow.Services.Orders;

/// <summary>
/// Sale ve Purchase onayı için yeni stok miktarlarını entity değiştirmeden hesaplayan bağımlılıksız karar bileşenidir.
/// </summary>
internal sealed class OrderStockConfirmationPlanner
{
    /// <summary>
    /// Durum taşımayan ve dış bağımlılığı bulunmayan bir stok planlayıcısı oluşturur.
    /// </summary>
    public OrderStockConfirmationPlanner()
    {
    }

    /// <summary>
    /// Sale için tüm satırlarda yeterlilik, Purchase için taşma kontrolü yaparak StockOut veya StockIn planı üretir. İlk kural ihlalinde mevcut hata kodu ve yapılandırılmış log bağlamını taşıyan ret kararı döndürür.
    /// </summary>
    public OrderStockConfirmationDecision CreatePlan(
        OrderType orderType,
        IReadOnlyCollection<OrderStockConfirmationItem> items)
    {
        return orderType switch
        {
            OrderType.Sale => CreateSalePlan(items),
            OrderType.Purchase => CreatePurchasePlan(items),
            _ => throw new ArgumentOutOfRangeException(nameof(orderType), orderType, null)
        };
    }

    private static OrderStockConfirmationDecision CreateSalePlan(
        IReadOnlyCollection<OrderStockConfirmationItem> items)
    {
        var newStockQuantities = new Dictionary<int, int>(items.Count);

        foreach (var item in items)
        {
            if (item.AvailableQuantity < item.RequestedQuantity)
            {
                return OrderStockConfirmationDecision.Rejected(
                    OrderServiceErrorCodes.InsufficientStock,
                    item);
            }

            newStockQuantities[item.ProductId] =
                item.AvailableQuantity - item.RequestedQuantity;
        }

        return OrderStockConfirmationDecision.Approved(new OrderStockConfirmationPlan(
            StockMovementType.StockOut,
            newStockQuantities));
    }

    private static OrderStockConfirmationDecision CreatePurchasePlan(
        IReadOnlyCollection<OrderStockConfirmationItem> items)
    {
        var newStockQuantities = new Dictionary<int, int>(items.Count);

        foreach (var item in items)
        {
            try
            {
                newStockQuantities[item.ProductId] = checked(
                    item.AvailableQuantity + item.RequestedQuantity);
            }
            catch (OverflowException)
            {
                return OrderStockConfirmationDecision.Rejected(
                    OrderServiceErrorCodes.StockQuantityOutOfRange,
                    item);
            }
        }

        return OrderStockConfirmationDecision.Approved(new OrderStockConfirmationPlan(
            StockMovementType.StockIn,
            newStockQuantities));
    }
}

/// <summary>
/// Planner'a verilen ürün kimliği ile istenen ve mevcut stok miktarlarının değişmez sayısal görünümüdür.
/// </summary>
internal readonly record struct OrderStockConfirmationItem(
    int ProductId,
    int RequestedQuantity,
    int AvailableQuantity);

/// <summary>
/// Bütün satırlar doğrulandıktan sonra uygulanabilecek hareket yönünü ve ürün bazlı yeni stokları taşır.
/// </summary>
internal sealed record OrderStockConfirmationPlan(
    StockMovementType MovementType,
    IReadOnlyDictionary<int, int> NewStockQuantities);

/// <summary>
/// Reddedilen stok planının kararlı hata kodunu ve sorgulanabilir ürün/miktar bağlamını taşır.
/// </summary>
internal sealed record OrderStockConfirmationFailure(
    string ErrorCode,
    int ProductId,
    int RequestedQuantity,
    int AvailableQuantity);

/// <summary>
/// Stok planlama sonucunu yalnızca onaylanmış plan veya ret ayrıntısından biri bulunacak şekilde kapsüller.
/// </summary>
internal sealed class OrderStockConfirmationDecision
{
    private OrderStockConfirmationDecision(
        OrderStockConfirmationPlan? plan,
        OrderStockConfirmationFailure? failure)
    {
        Plan = plan;
        Failure = failure;
    }

    public bool IsApproved => Plan is not null;

    public OrderStockConfirmationPlan? Plan { get; }

    public OrderStockConfirmationFailure? Failure { get; }

    /// <summary>
    /// Tüm satır kontrollerini geçen uygulanabilir stok planı için onay kararı oluşturur.
    /// </summary>
    public static OrderStockConfirmationDecision Approved(OrderStockConfirmationPlan plan)
    {
        return new OrderStockConfirmationDecision(plan, null);
    }

    /// <summary>
    /// Kuralı ihlal eden satırın ürün ve miktar bağlamıyla ret kararı oluşturur; uygulanabilir plan döndürmez.
    /// </summary>
    public static OrderStockConfirmationDecision Rejected(
        string errorCode,
        OrderStockConfirmationItem item)
    {
        return new OrderStockConfirmationDecision(
            null,
            new OrderStockConfirmationFailure(
                errorCode,
                item.ProductId,
                item.RequestedQuantity,
                item.AvailableQuantity));
    }
}
