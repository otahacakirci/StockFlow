using StockFlow.ViewModels.Categories;

namespace StockFlow.ViewModels.Products;

public sealed record ProductListPageViewModel(
    ProductListViewModel Products,
    IReadOnlyList<CategorySelectionOptionViewModel> Categories);
