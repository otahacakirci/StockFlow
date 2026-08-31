using StockFlow.Entities;

namespace StockFlow.ViewModels.StockMovements;

public sealed class StockMovementListQueryModel
{
    public int? ProductId { get; set; }

    public int? OrderId { get; set; }

    public StockMovementType? Type { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public StockMovementSortOrder SortOrder { get; set; } =
        StockMovementSortOrder.DateDescending;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
