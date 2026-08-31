using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

/// <summary>
/// Sipariş ekranlarının salt-okunur, filtreli ve projection tabanlı veri ihtiyaçlarını yönetir.
/// </summary>
internal sealed class OrderQueryService(ApplicationDbContext dbContext) : IOrderQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<ServiceResult<OrderListViewModel>> GetListAsync(
        OrderListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var orders = dbContext.Orders.AsNoTracking();

        if (normalizedQuery.Type.HasValue)
        {
            orders = orders.Where(order => order.Type == normalizedQuery.Type.Value);
        }

        if (normalizedQuery.Status.HasValue)
        {
            orders = orders.Where(order => order.Status == normalizedQuery.Status.Value);
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling((double)totalCount / normalizedQuery.PageSize);
        var page = totalPages == 0
            ? 1
            : Math.Min(normalizedQuery.Page, totalPages);

        var orderedOrders = normalizedQuery.SortOrder == OrderSortOrder.DateAscending
            ? orders.OrderBy(order => order.OrderDate).ThenBy(order => order.Id)
            : orders.OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.Id);

        var items = await orderedOrders
            .Skip((page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .Select(order => new OrderListItemViewModel(
                order.Id,
                order.OrderNumber,
                order.Type,
                order.Status,
                order.OrderDate,
                order.TotalAmount,
                order.Type == OrderType.Sale
                    ? order.Customer!.Name
                    : order.Supplier!.CompanyName,
                order.Items.Count))
            .ToListAsync(cancellationToken);

        return ServiceResult<OrderListViewModel>.Success(new OrderListViewModel(
            items,
            normalizedQuery.Type,
            normalizedQuery.Status,
            normalizedQuery.SortOrder,
            page,
            normalizedQuery.PageSize,
            totalCount,
            totalPages));
    }

    public async Task<ServiceResult<OrderDetailViewModel>> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(candidate => candidate.Id == orderId)
            .Select(candidate => new OrderDetailViewModel(
                candidate.Id,
                candidate.OrderNumber,
                candidate.Type,
                candidate.Status,
                candidate.OrderDate,
                candidate.TotalAmount,
                candidate.CustomerId,
                candidate.Customer == null ? null : candidate.Customer.Name,
                candidate.SupplierId,
                candidate.Supplier == null ? null : candidate.Supplier.CompanyName,
                candidate.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemViewModel(
                        item.Id,
                        item.ProductId,
                        item.Product.Name,
                        item.Product.Sku,
                        item.Quantity,
                        item.UnitPrice,
                        item.Quantity * item.UnitPrice))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return order is null
            ? OrderNotFound<OrderDetailViewModel>()
            : ServiceResult<OrderDetailViewModel>.Success(order);
    }

    public async Task<ServiceResult<OrderDraftEditViewModel>> GetDraftForEditAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(candidate => candidate.Id == orderId)
            .Select(candidate => new DraftEditProjection(
                candidate.Status,
                new OrderDraftEditViewModel(
                    candidate.Id,
                    candidate.OrderNumber,
                    candidate.OrderDate,
                    candidate.Type,
                    candidate.CustomerId,
                    candidate.SupplierId,
                    candidate.TotalAmount,
                    candidate.Items
                        .OrderBy(item => item.Id)
                        .Select(item => new OrderDraftEditItemViewModel(
                            item.ProductId,
                            item.Product.Name,
                            item.Product.Sku,
                            item.Quantity,
                            item.UnitPrice))
                        .ToList())))
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return OrderNotFound<OrderDraftEditViewModel>();
        }

        if (order.Status != OrderStatus.Draft)
        {
            return ServiceResult<OrderDraftEditViewModel>.Failure(new ServiceError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft,
                "Yalnızca taslak siparişler düzenlenebilir."));
        }

        return ServiceResult<OrderDraftEditViewModel>.Success(order.Model);
    }

    private static NormalizedOrderListQuery NormalizeQuery(OrderListQueryModel? query)
    {
        OrderType? type = query?.Type is { } requestedType && Enum.IsDefined(requestedType)
            ? requestedType
            : null;
        OrderStatus? status = query?.Status is { } requestedStatus && Enum.IsDefined(requestedStatus)
            ? requestedStatus
            : null;
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : OrderSortOrder.DateDescending;
        var page = query?.Page > 0 ? query.Page : 1;
        var pageSize = query?.PageSize > 0
            ? Math.Min(query.PageSize, MaximumPageSize)
            : DefaultPageSize;

        return new NormalizedOrderListQuery(type, status, sortOrder, page, pageSize);
    }

    private static ServiceResult<T> OrderNotFound<T>()
    {
        return ServiceResult<T>.Failure(new ServiceError(
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.OrderNotFound,
            "Sipariş bulunamadı."));
    }

    private sealed record NormalizedOrderListQuery(
        OrderType? Type,
        OrderStatus? Status,
        OrderSortOrder SortOrder,
        int Page,
        int PageSize);

    private sealed record DraftEditProjection(
        OrderStatus Status,
        OrderDraftEditViewModel Model);
}
