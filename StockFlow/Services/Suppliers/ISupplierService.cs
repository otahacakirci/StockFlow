using StockFlow.Services.Common;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Services.Suppliers;

/// <summary>
/// Tedarikçilerin güvenli giriş ve çıkış modelleriyle sorgulanmasını ve yönetilmesini tanımlar.
/// </summary>
public interface ISupplierService
{
    /// <summary>
    /// Tedarikçileri iletişim alanı araması, güvenli sıralama ve normalize sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<SupplierListViewModel>> GetListAsync(
        SupplierListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tedarikçiyi ilişkili sipariş sayısıyla döndürür; kayıt yoksa kategorili hata üretir.
    /// </summary>
    Task<ServiceResult<SupplierViewModel>> GetByIdAsync(
        int supplierId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sipariş formları için tedarikçileri yalnız kimlik ve şirket adı bilgisiyle sıralı olarak döndürür.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulanmış ve normalize edilmiş bilgilerle yeni tedarikçi oluşturur.
    /// </summary>
    Task<ServiceResult<SupplierViewModel>> CreateAsync(
        SupplierInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Var olan tedarikçinin doğrulanmış ve normalize edilmiş bilgilerini günceller.
    /// </summary>
    Task<ServiceResult<SupplierViewModel>> UpdateAsync(
        int supplierId,
        SupplierInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnızca ilişkili siparişi bulunmayan tedarikçiyi fiziksel olarak siler.
    /// </summary>
    Task<ServiceResult> DeleteAsync(
        int supplierId,
        CancellationToken cancellationToken = default);
}
