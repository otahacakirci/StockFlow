using StockFlow.Services.Common;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

public interface IOrderService
{
    Task<ServiceResult<OrderMutationResult>> CreateDraftAsync(
        OrderDraftInputModel input,
        string createdByUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderMutationResult>> UpdateDraftAsync(
        int orderId,
        OrderDraftInputModel input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderMutationResult>> ConfirmDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderMutationResult>> CancelDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
