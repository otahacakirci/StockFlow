using StockFlow.Services.Common;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Services.StockMovements;

/// <summary>
/// Stok hareketlerinin güvenli, salt-okunur liste ve detay sorgularını tanımlar.
/// </summary>
public interface IStockMovementQueryService
{
    /// <summary>
    /// Stok hareketlerini ilişki/tür/tarih filtreleri, güvenli sıralama ve normalize sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<StockMovementListViewModel>> GetListAsync(
        StockMovementListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stok hareketini Product ve Order projection'larıyla döndürür; kayıt yoksa kategorili hata üretir.
    /// </summary>
    Task<ServiceResult<StockMovementViewModel>> GetByIdAsync(
        int stockMovementId,
        CancellationToken cancellationToken = default);
}
