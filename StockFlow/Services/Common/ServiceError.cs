namespace StockFlow.Services.Common;

public sealed record ServiceError(
    ServiceErrorCategory Category,
    string Code,
    string Message);
