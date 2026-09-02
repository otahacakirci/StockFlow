using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Orders;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class OrdersController(
    IOrderQueryService orderQueryService,
    ILogger<OrdersController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        OrderListQueryModel query,
        CancellationToken cancellationToken)
    {
        var result = await orderQueryService.GetListAsync(query, cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? View(result.Value)
            : UnexpectedFailure("list", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await orderQueryService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsOrderNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    private static bool IsOrderNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == OrderServiceErrorCodes.OrderNotFound;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? orderId = null)
    {
        logger.LogError(
            "Order MVC operation {Operation} returned unexpected result {ErrorCode} for order {OrderId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "order.unexpected_result",
            orderId,
            HttpContext.TraceIdentifier);
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", CreateErrorViewModel());
    }

    private ErrorViewModel CreateErrorViewModel()
    {
        return new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };
    }
}
