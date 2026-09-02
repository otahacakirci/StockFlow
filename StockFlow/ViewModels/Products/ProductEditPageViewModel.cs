using StockFlow.ViewModels.Categories;

namespace StockFlow.ViewModels.Products;

public sealed record ProductEditPageViewModel(
    int Id,
    int CurrentStockQuantity,
    ProductUpdateInputModel Input,
    IReadOnlyList<CategorySelectionOptionViewModel> Categories,
    string ReturnUrl);
