namespace StockFlow.Services.StockMovements;

/// <summary>
/// StockMovement sorgularındaki beklenen hatalar için kararlı kodları toplar.
/// </summary>
public static class StockMovementQueryServiceErrorCodes
{
    public const string StockMovementNotFound = "stock_movement.not_found";
    public const string InvalidDateRange = "stock_movement.date_range_invalid";
}
