using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.StockMovements;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class StockMovementsController(
    IStockMovementQueryService stockMovementQueryService,
    ILogger<StockMovementsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        StockMovementListQueryModel query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(ToPageViewModel(query));
        }

        var result = await stockMovementQueryService.GetListAsync(query, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return View(ToPageViewModel(result.Value));
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(ToPageViewModel(query));
        }

        return UnexpectedFailure("list", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await stockMovementQueryService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsStockMovementNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    private static StockMovementListPageViewModel ToPageViewModel(
        StockMovementListQueryModel query)
    {
        return new StockMovementListPageViewModel(
            query.ProductId,
            query.OrderId,
            query.Type,
            query.StartDate,
            query.EndDate,
            query.SortOrder,
            query.PageSize,
            Results: null);
    }

    private static StockMovementListPageViewModel ToPageViewModel(
        StockMovementListViewModel results)
    {
        return new StockMovementListPageViewModel(
            results.ProductId,
            results.OrderId,
            results.Type,
            results.StartDate,
            results.EndDate,
            results.SortOrder,
            results.PageSize,
            results);
    }

    private static bool IsStockMovementNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == StockMovementQueryServiceErrorCodes.StockMovementNotFound;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? stockMovementId = null)
    {
        logger.LogError(
            "StockMovement MVC operation {Operation} returned unexpected result {ErrorCode} for movement {StockMovementId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "stock_movement.unexpected_result",
            stockMovementId,
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
