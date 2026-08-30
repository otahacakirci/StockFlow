using StockFlow.Services.Common;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Services.Customers;

/// <summary>
/// Müşterilerin güvenli giriş ve çıkış modelleriyle sorgulanmasını ve yönetilmesini tanımlar.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Müşterileri iletişim alanı araması, güvenli sıralama ve normalize sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<CustomerListViewModel>> GetListAsync(
        CustomerListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Müşteriyi ilişkili sipariş sayısıyla döndürür; kayıt yoksa kategorili hata üretir.
    /// </summary>
    Task<ServiceResult<CustomerViewModel>> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sipariş formları için müşterileri yalnız kimlik ve ad bilgisiyle sıralı olarak döndürür.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulanmış ve normalize edilmiş bilgilerle yeni müşteri oluşturur.
    /// </summary>
    Task<ServiceResult<CustomerViewModel>> CreateAsync(
        CustomerInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Var olan müşterinin doğrulanmış ve normalize edilmiş bilgilerini günceller.
    /// </summary>
    Task<ServiceResult<CustomerViewModel>> UpdateAsync(
        int customerId,
        CustomerInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnızca ilişkili siparişi bulunmayan müşteriyi fiziksel olarak siler.
    /// </summary>
    Task<ServiceResult> DeleteAsync(
        int customerId,
        CancellationToken cancellationToken = default);
}
