using StockFlow.ViewModels.Categories;

namespace StockFlow.ViewModels.Products;

public sealed record ProductCreatePageViewModel(
    ProductCreateInputModel Input,
    IReadOnlyList<CategorySelectionOptionViewModel> Categories);
