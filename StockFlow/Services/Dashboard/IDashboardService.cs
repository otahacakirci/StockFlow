using StockFlow.Services.Common;
using StockFlow.ViewModels.Dashboard;

namespace StockFlow.Services.Dashboard;

/// <summary>
/// Dashboard metrikleri ve son siparişler için salt-okunur Service sözleşmesini tanımlar.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Temel kayıt/stok/satış metriklerini ve son sipariş özetlerini güvenli bir sonuç modeliyle döndürür.
    /// </summary>
    Task<ServiceResult<DashboardViewModel>> GetAsync(
        CancellationToken cancellationToken = default);
}
