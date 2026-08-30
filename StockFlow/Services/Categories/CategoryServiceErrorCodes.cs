namespace StockFlow.Services.Categories;

/// <summary>
/// Category Service işlemlerinin beklenen hataları için güvenle eşleştirilebilen kararlı kodları toplar.
/// </summary>
public static class CategoryServiceErrorCodes
{
    public const string InputRequired = "category.input_required";
    public const string NameRequired = "category.name_required";
    public const string NameTooLong = "category.name_too_long";
    public const string CategoryNotFound = "category.not_found";
    public const string CategoryHasProducts = "category.has_products";
}
