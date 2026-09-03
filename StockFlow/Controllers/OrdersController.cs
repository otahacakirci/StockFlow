using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Entities;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.Services.Suppliers;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class OrdersController(
    IOrderQueryService orderQueryService,
    IOrderService orderService,
    ICustomerService customerService,
    ISupplierService supplierService,
    IProductService productService,
    ILogger<OrdersController> logger) : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";

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

    [HttpGet]
    public async Task<IActionResult> Create(
        OrderType? type,
        CancellationToken cancellationToken)
    {
        var requestedType = type is { } candidate && Enum.IsDefined(candidate)
            ? candidate
            : OrderType.Sale;
        var input = new OrderDraftInputModel
        {
            Type = requestedType,
            Items = [new OrderItemInputModel { Quantity = 1 }]
        };

        return await FormViewAsync(
            nameof(Create),
            input,
            orderId: null,
            orderNumber: null,
            currentTotalAmount: null,
            GetLocalReturnUrl(returnUrl: null, orderId: null),
            selectAvailableType: true,
            cancellationToken,
            "create_options");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = nameof(OrderDraftFormPageViewModel.Input))] OrderDraftInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FormViewAsync(
                nameof(Create),
                input,
                orderId: null,
                orderNumber: null,
                currentTotalAmount: null,
                GetLocalReturnUrl(returnUrl: null, orderId: null),
                selectAvailableType: false,
                cancellationToken,
                "create_validation_options");
        }

        var createdByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await orderService.CreateDraftAsync(
            input,
            createdByUserId,
            cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Taslak sipariş başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Details), new { id = result.Value.OrderId });
        }

        if (IsDraftFormError(result.Error))
        {
            AddDraftFormError(result.Error!, input);
            return await FormViewAsync(
                nameof(Create),
                input,
                orderId: null,
                orderNumber: null,
                currentTotalAmount: null,
                GetLocalReturnUrl(returnUrl: null, orderId: null),
                selectAvailableType: false,
                cancellationToken,
                "create_error_options");
        }

        return UnexpectedFailure("create", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await orderQueryService.GetDraftForEditAsync(id, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            if (IsOrderNotFound(result.Error))
            {
                return NotFoundView();
            }

            return IsOrderNotDraft(result.Error)
                ? await DraftConflictAsync(id, result.Error!, cancellationToken, "edit_conflict")
                : UnexpectedFailure("edit", result.Error, id);
        }

        return await FormViewAsync(
            nameof(Edit),
            ToInputModel(result.Value),
            result.Value.Id,
            result.Value.OrderNumber,
            result.Value.TotalAmount,
            GetLocalReturnUrl(returnUrl, result.Value.Id),
            selectAvailableType: false,
            cancellationToken,
            "edit_options");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        [Bind(Prefix = nameof(OrderDraftFormPageViewModel.Input))] OrderDraftInputModel input,
        CancellationToken cancellationToken)
    {
        var safeReturnUrl = GetLocalReturnUrl(returnUrl, id);

        if (!ModelState.IsValid)
        {
            return await ReloadEditFormAsync(
                id,
                input,
                safeReturnUrl,
                cancellationToken,
                "edit_validation");
        }

        var result = await orderService.UpdateDraftAsync(id, input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Taslak sipariş başarıyla güncellendi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.OrderId });
        }

        if (IsOrderNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (IsOrderNotDraft(result.Error))
        {
            return await DraftConflictAsync(id, result.Error!, cancellationToken, "update_conflict");
        }

        if (IsDraftFormError(result.Error))
        {
            AddDraftFormError(result.Error!, input);
            return await ReloadEditFormAsync(
                id,
                input,
                safeReturnUrl,
                cancellationToken,
                "edit_error");
        }

        return UnexpectedFailure("update", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public Task<IActionResult> Confirm(
        int id,
        CancellationToken cancellationToken)
    {
        return DraftActionViewAsync(nameof(Confirm), id, cancellationToken);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ActionName(nameof(Confirm))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await orderService.ConfirmDraftAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Sipariş başarıyla onaylandı.";
            return RedirectToAction(nameof(Details), new { id = result.Value.OrderId });
        }

        return await DraftActionFailureAsync(
            nameof(Confirm),
            id,
            result.Error,
            cancellationToken,
            "confirm");
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public Task<IActionResult> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        return DraftActionViewAsync(nameof(Cancel), id, cancellationToken);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ActionName(nameof(Cancel))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await orderService.CancelDraftAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Sipariş başarıyla iptal edildi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.OrderId });
        }

        return await DraftActionFailureAsync(
            nameof(Cancel),
            id,
            result.Error,
            cancellationToken,
            "cancel");
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        return DraftActionViewAsync(nameof(Delete), id, cancellationToken);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await orderService.DeleteDraftAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessMessageKey] = "Taslak sipariş başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        return await DraftActionFailureAsync(
            nameof(Delete),
            id,
            result.Error,
            cancellationToken,
            "delete");
    }

    private async Task<IActionResult> DraftActionViewAsync(
        string viewName,
        int orderId,
        CancellationToken cancellationToken)
    {
        var result = await orderQueryService.GetByIdAsync(orderId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return IsOrderNotFound(result.Error)
                ? NotFoundView()
                : UnexpectedFailure(viewName.ToLowerInvariant() + "_confirmation", result.Error, orderId);
        }

        if (result.Value.Status != OrderStatus.Draft)
        {
            ModelState.AddModelError(
                string.Empty,
                "Bu işlem yalnızca taslak siparişlerde yapılabilir.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(nameof(Details), result.Value);
        }

        return View(viewName, result.Value);
    }

    private async Task<IActionResult> DraftActionFailureAsync(
        string viewName,
        int orderId,
        ServiceError? error,
        CancellationToken cancellationToken,
        string operation)
    {
        if (error?.Category == ServiceErrorCategory.NotFound)
        {
            return NotFoundView();
        }

        if (IsOrderNotDraft(error))
        {
            return await DraftConflictAsync(orderId, error!, cancellationToken, operation + "_conflict");
        }

        if (error?.Category is not (
            ServiceErrorCategory.Validation or ServiceErrorCategory.BusinessRule))
        {
            return UnexpectedFailure(operation, error, orderId);
        }

        var detailResult = await orderQueryService.GetByIdAsync(orderId, cancellationToken);
        if (!detailResult.IsSuccess || detailResult.Value is null)
        {
            return IsOrderNotFound(detailResult.Error)
                ? NotFoundView()
                : UnexpectedFailure(operation + "_reload", detailResult.Error, orderId);
        }

        ModelState.AddModelError(string.Empty, error.Message);
        Response.StatusCode = error.Category == ServiceErrorCategory.Validation
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status409Conflict;
        return View(viewName, detailResult.Value);
    }

    private async Task<IActionResult> ReloadEditFormAsync(
        int orderId,
        OrderDraftInputModel input,
        string returnUrl,
        CancellationToken cancellationToken,
        string operation)
    {
        var draftResult = await orderQueryService.GetDraftForEditAsync(orderId, cancellationToken);
        if (!draftResult.IsSuccess || draftResult.Value is null)
        {
            if (IsOrderNotFound(draftResult.Error))
            {
                return NotFoundView();
            }

            return IsOrderNotDraft(draftResult.Error)
                ? await DraftConflictAsync(
                    orderId,
                    draftResult.Error!,
                    cancellationToken,
                    operation + "_conflict")
                : UnexpectedFailure(operation + "_draft", draftResult.Error, orderId);
        }

        return await FormViewAsync(
            nameof(Edit),
            input,
            draftResult.Value.Id,
            draftResult.Value.OrderNumber,
            draftResult.Value.TotalAmount,
            returnUrl,
            selectAvailableType: false,
            cancellationToken,
            operation + "_options");
    }

    private async Task<IActionResult> FormViewAsync(
        string viewName,
        OrderDraftInputModel input,
        int? orderId,
        string? orderNumber,
        decimal? currentTotalAmount,
        string returnUrl,
        bool selectAvailableType,
        CancellationToken cancellationToken,
        string operation)
    {
        var customerResult = await customerService.GetSelectionOptionsAsync(cancellationToken);
        if (!customerResult.IsSuccess || customerResult.Value is null)
        {
            return UnexpectedFailure(operation + "_customers", customerResult.Error, orderId);
        }

        var supplierResult = await supplierService.GetSelectionOptionsAsync(cancellationToken);
        if (!supplierResult.IsSuccess || supplierResult.Value is null)
        {
            return UnexpectedFailure(operation + "_suppliers", supplierResult.Error, orderId);
        }

        var productResult = await productService.GetSelectionOptionsAsync(cancellationToken);
        if (!productResult.IsSuccess || productResult.Value is null)
        {
            return UnexpectedFailure(operation + "_products", productResult.Error, orderId);
        }

        if (selectAvailableType)
        {
            if (input.Type == OrderType.Sale
                && customerResult.Value.Count == 0
                && supplierResult.Value.Count > 0)
            {
                input.Type = OrderType.Purchase;
            }
            else if (input.Type == OrderType.Purchase
                && supplierResult.Value.Count == 0
                && customerResult.Value.Count > 0)
            {
                input.Type = OrderType.Sale;
            }
        }

        if (input.Items is null || input.Items.Count == 0)
        {
            input.Items = [new OrderItemInputModel { Quantity = 1 }];
        }

        return View(viewName, new OrderDraftFormPageViewModel(
            orderId,
            orderNumber,
            currentTotalAmount,
            input,
            customerResult.Value,
            supplierResult.Value,
            productResult.Value,
            returnUrl));
    }

    private async Task<IActionResult> DraftConflictAsync(
        int orderId,
        ServiceError error,
        CancellationToken cancellationToken,
        string operation)
    {
        var detailResult = await orderQueryService.GetByIdAsync(orderId, cancellationToken);
        if (!detailResult.IsSuccess || detailResult.Value is null)
        {
            return IsOrderNotFound(detailResult.Error)
                ? NotFoundView()
                : UnexpectedFailure(operation + "_details", detailResult.Error, orderId);
        }

        ModelState.AddModelError(string.Empty, error.Message);
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View(nameof(Details), detailResult.Value);
    }

    private string GetLocalReturnUrl(string? returnUrl, int? orderId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Url.Content(returnUrl);
        }

        return orderId.HasValue
            ? Url.Action(nameof(Details), new { id = orderId.Value }) ?? "/Orders"
            : Url.Action(nameof(Index)) ?? "/Orders";
    }

    private static OrderDraftInputModel ToInputModel(OrderDraftEditViewModel draft)
    {
        return new OrderDraftInputModel
        {
            Type = draft.Type,
            CustomerId = draft.CustomerId,
            SupplierId = draft.SupplierId,
            Items = draft.Items.Select(item => new OrderItemInputModel
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            }).ToList()
        };
    }

    private void AddDraftFormError(ServiceError error, OrderDraftInputModel input)
    {
        var key = error.Code switch
        {
            OrderServiceErrorCodes.InvalidOrderType => nameof(OrderDraftInputModel.Type),
            OrderServiceErrorCodes.InvalidParty => input.Type == OrderType.Purchase
                ? nameof(OrderDraftInputModel.SupplierId)
                : nameof(OrderDraftInputModel.CustomerId),
            CustomerServiceErrorCodes.CustomerNotFound => nameof(OrderDraftInputModel.CustomerId),
            SupplierServiceErrorCodes.SupplierNotFound => nameof(OrderDraftInputModel.SupplierId),
            OrderServiceErrorCodes.ItemsRequired
                or OrderServiceErrorCodes.InvalidProduct
                or OrderServiceErrorCodes.InvalidQuantity
                or OrderServiceErrorCodes.DuplicateProduct
                or ProductServiceErrorCodes.ProductNotFound
                or ProductServiceErrorCodes.PriceInvalid
                or OrderServiceErrorCodes.TotalOutOfRange => nameof(OrderDraftInputModel.Items),
            _ => string.Empty
        };

        var modelStateKey = string.IsNullOrEmpty(key)
            ? string.Empty
            : $"{nameof(OrderDraftFormPageViewModel.Input)}.{key}";
        ModelState.AddModelError(modelStateKey, error.Message);
    }

    private static bool IsDraftFormError(ServiceError? error)
    {
        return error?.Code is
            OrderServiceErrorCodes.InputRequired
            or OrderServiceErrorCodes.InvalidOrderType
            or OrderServiceErrorCodes.InvalidParty
            or CustomerServiceErrorCodes.CustomerNotFound
            or SupplierServiceErrorCodes.SupplierNotFound
            or OrderServiceErrorCodes.ItemsRequired
            or OrderServiceErrorCodes.InvalidProduct
            or OrderServiceErrorCodes.InvalidQuantity
            or OrderServiceErrorCodes.DuplicateProduct
            or ProductServiceErrorCodes.ProductNotFound
            or ProductServiceErrorCodes.PriceInvalid
            or OrderServiceErrorCodes.TotalOutOfRange;
    }

    private static bool IsOrderNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == OrderServiceErrorCodes.OrderNotFound;
    }

    private static bool IsOrderNotDraft(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.BusinessRule
            && error.Code == OrderServiceErrorCodes.OrderNotDraft;
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
