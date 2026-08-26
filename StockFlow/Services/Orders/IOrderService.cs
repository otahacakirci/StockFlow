using StockFlow.Services.Common;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

/// <summary>
/// Draft sipariş yaşam döngüsünü ve onay sırasında atomik stok etkisini yöneten Service sözleşmesini tanımlar.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Sunucu fiyat snapshot'larıyla Sale veya Purchase Draft oluşturur; geçersiz taraf, kullanıcı ya da kalemde kategorili hata döndürür. Draft oluşturma stok veya hareket üretmez.
    /// </summary>
    Task<ServiceResult<OrderMutationResult>> CreateDraftAsync(
        OrderDraftInputModel input,
        string createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnız Draft siparişin tarafını ve kalemlerini günceller; kalan satırların fiyat snapshot'ını koruyup yeni ürünlerde güncel fiyatı kullanır. Eksik veya terminal siparişte beklenen hata döndürür ve stok değiştirmez.
    /// </summary>
    Task<ServiceResult<OrderMutationResult>> UpdateDraftAsync(
        int orderId,
        OrderDraftInputModel input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Geçerli Draft'ı Sale için StockOut, Purchase için StockIn etkisiyle atomik olarak onaylar. Herhangi bir doğrulama veya stok kuralı ihlalinde başarısız sonuç döner; kalıcılaştırma hatasında transaction geri alınır.
    /// </summary>
    Task<ServiceResult<OrderMutationResult>> ConfirmDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnız Draft siparişi stok hareketi üretmeden terminal Cancelled durumuna geçirir; eksik veya terminal siparişte beklenen hata döndürür.
    /// </summary>
    Task<ServiceResult<OrderMutationResult>> CancelDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnız hareket geçmişi bulunmayan Draft siparişi ve kalemlerini fiziksel olarak siler. Eksik, terminal veya hareketli siparişte kategorili hata döndürür.
    /// </summary>
    Task<ServiceResult> DeleteDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
