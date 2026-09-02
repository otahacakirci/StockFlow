namespace StockFlow.ViewModels.Categories;

public sealed record CategoryEditPageViewModel(
    int Id,
    CategoryInputModel Input,
    string ReturnUrl);
