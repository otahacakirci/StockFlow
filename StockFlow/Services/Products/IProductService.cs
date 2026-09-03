using StockFlow.Services.Common;
using StockFlow.ViewModels.Products;

namespace StockFlow.Services.Products;

/// <summary>
/// Ürünlerin güvenli giriş ve çıkış modelleriyle sorgulanmasını ve yönetilmesini tanımlar.
/// </summary>
public interface IProductService
{
    /// <summary>
    /// Ürünleri arama, kategori/düşük stok filtresi, güvenli sıralama ve normalize sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<ProductListViewModel>> GetListAsync(
        ProductListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ürünü kategori bilgisi ve hesaplanan düşük stok durumuyla döndürür.
    /// </summary>
    Task<ServiceResult<ProductViewModel>> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sipariş formları için ürünleri yalnız kimlik, ad ve SKU bilgisiyle sıralı olarak döndürür.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<ProductSelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulanmış ürün bilgileri ve nonnegative başlangıç stoğuyla yeni ürün oluşturur.
    /// </summary>
    Task<ServiceResult<ProductViewModel>> CreateAsync(
        ProductCreateInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ürünün düzenlenebilir alanlarını mevcut StockQuantity değerini değiştirmeden günceller.
    /// </summary>
    Task<ServiceResult<ProductViewModel>> UpdateAsync(
        int productId,
        ProductUpdateInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnız OrderItem veya StockMovement geçmişi bulunmayan ürünü fiziksel olarak siler.
    /// </summary>
    Task<ServiceResult> DeleteAsync(
        int productId,
        CancellationToken cancellationToken = default);
}
