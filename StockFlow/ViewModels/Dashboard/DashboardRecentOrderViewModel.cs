using StockFlow.Entities;

namespace StockFlow.ViewModels.Dashboard;

public sealed record DashboardRecentOrderViewModel(
    int Id,
    string OrderNumber,
    OrderType Type,
    OrderStatus Status,
    DateTime OrderDate,
    decimal TotalAmount,
    string PartyName);
