using StockFlow.Services.Common;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Services.Categories;

/// <summary>
/// Kategorilerin güvenli giriş ve çıkış modelleriyle sorgulanmasını ve yönetilmesini tanımlar.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Kategorileri ad araması, güvenli sıralama ve normalize edilmiş sunucu taraflı sayfalama ile listeler.
    /// </summary>
    Task<ServiceResult<CategoryListViewModel>> GetListAsync(
        CategoryListQueryModel? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kategoriyi bağlı ürün sayısıyla döndürür; kayıt yoksa kategorili hata üretir.
    /// </summary>
    Task<ServiceResult<CategoryViewModel>> GetByIdAsync(
        int categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ürün filtreleri ve formları için kategorileri yalnız kimlik ve ad bilgisiyle sıralı olarak döndürür.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulanmış ve normalize edilmiş adla yeni kategori oluşturur.
    /// </summary>
    Task<ServiceResult<CategoryViewModel>> CreateAsync(
        CategoryInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Var olan kategorinin adını doğrulanmış ve normalize edilmiş değerle günceller.
    /// </summary>
    Task<ServiceResult<CategoryViewModel>> UpdateAsync(
        int categoryId,
        CategoryInputModel? input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnızca bağlı ürünü bulunmayan kategoriyi fiziksel olarak siler.
    /// </summary>
    Task<ServiceResult> DeleteAsync(
        int categoryId,
        CancellationToken cancellationToken = default);
}
