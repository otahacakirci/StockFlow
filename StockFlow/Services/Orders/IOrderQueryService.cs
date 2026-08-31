using StockFlow.Services.Common;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

/// <summary>
/// Sipariş listesi, detayı ve Draft düzenleme ekranı için salt-okunur sorgu sözleşmesini tanımlar.
/// </summary>
public interface IOrderQueryService
{
    /// <summary>
    /// Siparişleri tür/durum filtresi, güvenli tarih sıralaması ve normalize sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<OrderListViewModel>> GetListAsync(
        OrderListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Siparişi taraf, kalem ve Product projection'larıyla döndürür; kayıt yoksa kategorili hata üretir.
    /// </summary>
    Task<ServiceResult<OrderDetailViewModel>> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnız Draft siparişin düzenleme ekranında kullanılacak mevcut taraf ve kalem verilerini döndürür.
    /// </summary>
    Task<ServiceResult<OrderDraftEditViewModel>> GetDraftForEditAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
