namespace StockFlow.ViewModels.Dashboard;

public sealed record DashboardViewModel(
    int TotalProductCount,
    int LowStockProductCount,
    int TotalCustomerCount,
    int TotalSupplierCount,
    int TotalOrderCount,
    decimal ConfirmedSaleTotalAmount,
    IReadOnlyList<DashboardRecentOrderViewModel> RecentOrders);
