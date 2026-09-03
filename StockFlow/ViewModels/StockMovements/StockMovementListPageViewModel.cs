using StockFlow.Entities;

namespace StockFlow.ViewModels.StockMovements;

public sealed record StockMovementListPageViewModel(
    int? ProductId,
    int? OrderId,
    StockMovementType? Type,
    DateOnly? StartDate,
    DateOnly? EndDate,
    StockMovementSortOrder SortOrder,
    int PageSize,
    StockMovementListViewModel? Results);
