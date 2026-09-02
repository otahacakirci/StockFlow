namespace StockFlow.ViewModels.Products;

public sealed record ProductViewModel(
    int Id,
    string Name,
    string Sku,
    decimal Price,
    int StockQuantity,
    int MinimumStockQuantity,
    int CategoryId,
    string CategoryName,
    bool IsLowStock,
    bool CanDelete);
