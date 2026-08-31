using StockFlow.Entities;

namespace StockFlow.ViewModels.StockMovements;

public sealed record StockMovementListViewModel(
    IReadOnlyList<StockMovementViewModel> Items,
    int? ProductId,
    int? OrderId,
    StockMovementType? Type,
    DateOnly? StartDate,
    DateOnly? EndDate,
    StockMovementSortOrder SortOrder,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
