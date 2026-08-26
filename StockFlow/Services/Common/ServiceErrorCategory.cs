namespace StockFlow.Services.Common;

/// <summary>
/// Beklenen Service hatalarını giriş doğrulama, eksik kayıt ve iş kuralı ihlali olarak sınıflandırır.
/// </summary>
public enum ServiceErrorCategory
{
    Validation = 1,
    NotFound = 2,
    BusinessRule = 3
}
