using StockFlow.Entities;

namespace StockFlow.ViewModels.StockMovements;

public sealed record StockMovementViewModel(
    int Id,
    int ProductId,
    string ProductName,
    string ProductSku,
    int OrderId,
    string OrderNumber,
    StockMovementType Type,
    int Quantity,
    string Description,
    DateTimeOffset MovementDateUtc);
