using StockFlow.ViewModels.Customers;
using StockFlow.ViewModels.Products;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.ViewModels.Orders;

public sealed record OrderDraftFormPageViewModel(
    int? OrderId,
    string? OrderNumber,
    decimal? CurrentTotalAmount,
    OrderDraftInputModel Input,
    IReadOnlyList<CustomerSelectionOptionViewModel> Customers,
    IReadOnlyList<SupplierSelectionOptionViewModel> Suppliers,
    IReadOnlyList<ProductSelectionOptionViewModel> Products,
    string ReturnUrl);
