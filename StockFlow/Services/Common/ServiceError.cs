namespace StockFlow.Services.Common;

/// <summary>
/// Service katmanındaki beklenen bir hatanın kategorisini, kararlı kodunu ve güvenli kullanıcı mesajını taşır.
/// </summary>
public sealed record ServiceError(
    ServiceErrorCategory Category,
    string Code,
    string Message);
