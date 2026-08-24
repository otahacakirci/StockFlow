namespace StockFlow.Entities;

public enum OrderType
{
    Sale = 1,
    Purchase = 2
}

public enum OrderStatus
{
    Draft = 1,
    Confirmed = 2,
    Cancelled = 3
}

public enum StockMovementType
{
    StockIn = 1,
    StockOut = 2
}
