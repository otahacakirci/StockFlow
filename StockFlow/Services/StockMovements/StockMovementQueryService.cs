using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Services.StockMovements;

/// <summary>
/// Stok hareketi ekranlarının SQL taraflı filtrelenmiş, sayfalanmış ve salt-okunur veri ihtiyaçlarını yönetir.
/// </summary>
internal sealed class StockMovementQueryService(ApplicationDbContext dbContext)
    : IStockMovementQueryService
{
    public async Task<ServiceResult<StockMovementListViewModel>> GetListAsync(
        StockMovementListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        if (query?.StartDate is { } startDate
            && query.EndDate is { } endDate
            && startDate > endDate)
        {
            return ServiceResult<StockMovementListViewModel>.Failure(new ServiceError(
                ServiceErrorCategory.Validation,
                StockMovementQueryServiceErrorCodes.InvalidDateRange,
                "Başlangıç tarihi bitiş tarihinden sonra olamaz."));
        }

        var normalizedQuery = NormalizeQuery(query);
        var movements = dbContext.StockMovements.AsNoTracking();

        if (normalizedQuery.ProductId.HasValue)
        {
            movements = movements.Where(movement =>
                movement.ProductId == normalizedQuery.ProductId.Value);
        }

        if (normalizedQuery.OrderId.HasValue)
        {
            movements = movements.Where(movement =>
                movement.OrderId == normalizedQuery.OrderId.Value);
        }

        if (normalizedQuery.Type.HasValue)
        {
            movements = movements.Where(movement =>
                movement.Type == normalizedQuery.Type.Value);
        }

        if (normalizedQuery.StartDate.HasValue)
        {
            var startUtc = normalizedQuery.StartDate.Value.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc);
            movements = movements.Where(movement => movement.MovementDate >= startUtc);
        }

        if (normalizedQuery.EndDate.HasValue)
        {
            var endUtc = normalizedQuery.EndDate.Value.ToDateTime(
                TimeOnly.MaxValue,
                DateTimeKind.Utc);
            movements = movements.Where(movement => movement.MovementDate <= endUtc);
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        var page = ListPagingPolicy.Resolve(normalizedQuery.PageRequest, totalCount);

        var orderedMovements = normalizedQuery.SortOrder == StockMovementSortOrder.DateAscending
            ? movements.OrderBy(movement => movement.MovementDate).ThenBy(movement => movement.Id)
            : movements.OrderByDescending(movement => movement.MovementDate)
                .ThenByDescending(movement => movement.Id);

        var projections = await orderedMovements
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(movement => new StockMovementProjection(
                movement.Id,
                movement.ProductId,
                movement.Product.Name,
                movement.Product.Sku,
                movement.OrderId,
                movement.Order.OrderNumber,
                movement.Type,
                movement.Quantity,
                movement.Description,
                movement.MovementDate))
            .ToListAsync(cancellationToken);
        var items = projections.Select(ToViewModel).ToList();

        return ServiceResult<StockMovementListViewModel>.Success(new StockMovementListViewModel(
            items,
            normalizedQuery.ProductId,
            normalizedQuery.OrderId,
            normalizedQuery.Type,
            normalizedQuery.StartDate,
            normalizedQuery.EndDate,
            normalizedQuery.SortOrder,
            page.Page,
            page.PageSize,
            totalCount,
            page.TotalPages));
    }

    public async Task<ServiceResult<StockMovementViewModel>> GetByIdAsync(
        int stockMovementId,
        CancellationToken cancellationToken = default)
    {
        var projection = await dbContext.StockMovements
            .AsNoTracking()
            .Where(movement => movement.Id == stockMovementId)
            .Select(movement => new StockMovementProjection(
                movement.Id,
                movement.ProductId,
                movement.Product.Name,
                movement.Product.Sku,
                movement.OrderId,
                movement.Order.OrderNumber,
                movement.Type,
                movement.Quantity,
                movement.Description,
                movement.MovementDate))
            .SingleOrDefaultAsync(cancellationToken);

        return projection is null
            ? ServiceResult<StockMovementViewModel>.Failure(new ServiceError(
                ServiceErrorCategory.NotFound,
                StockMovementQueryServiceErrorCodes.StockMovementNotFound,
                "Stok hareketi bulunamadı."))
            : ServiceResult<StockMovementViewModel>.Success(ToViewModel(projection));
    }

    private static NormalizedStockMovementListQuery NormalizeQuery(
        StockMovementListQueryModel? query)
    {
        var productId = query?.ProductId > 0 ? query.ProductId : null;
        var orderId = query?.OrderId > 0 ? query.OrderId : null;
        StockMovementType? type = query?.Type is { } requestedType
            && Enum.IsDefined(requestedType)
                ? requestedType
                : null;
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : StockMovementSortOrder.DateDescending;
        var pageRequest = ListPagingPolicy.Normalize(query?.Page, query?.PageSize);

        return new NormalizedStockMovementListQuery(
            productId,
            orderId,
            type,
            query?.StartDate,
            query?.EndDate,
            sortOrder,
            pageRequest);
    }

    private static StockMovementViewModel ToViewModel(StockMovementProjection projection)
    {
        var utcMovementDate = new DateTimeOffset(
            DateTime.SpecifyKind(projection.MovementDate, DateTimeKind.Utc));

        return new StockMovementViewModel(
            projection.Id,
            projection.ProductId,
            projection.ProductName,
            projection.ProductSku,
            projection.OrderId,
            projection.OrderNumber,
            projection.Type,
            projection.Quantity,
            projection.Description,
            utcMovementDate);
    }

    private sealed record NormalizedStockMovementListQuery(
        int? ProductId,
        int? OrderId,
        StockMovementType? Type,
        DateOnly? StartDate,
        DateOnly? EndDate,
        StockMovementSortOrder SortOrder,
        NormalizedPageRequest PageRequest);

    private sealed record StockMovementProjection(
        int Id,
        int ProductId,
        string ProductName,
        string ProductSku,
        int OrderId,
        string OrderNumber,
        StockMovementType Type,
        int Quantity,
        string Description,
        DateTime MovementDate);
}
