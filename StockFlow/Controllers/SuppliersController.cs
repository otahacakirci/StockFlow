using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Suppliers;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public sealed class SuppliersController(
    ISupplierService supplierService,
    ILogger<SuppliersController> logger) : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";

    [HttpGet]
    public async Task<IActionResult> Index(
        SupplierListQueryModel query,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetListAsync(query, cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? View(result.Value)
            : UnexpectedFailure("list", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsSupplierNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new SupplierInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SupplierInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await supplierService.CreateAsync(input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Tedarikçi başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            AddValidationError(result.Error);
            return View(input);
        }

        return UnexpectedFailure("create", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(new SupplierEditPageViewModel(
                result.Value.Id,
                new SupplierInputModel
                {
                    CompanyName = result.Value.CompanyName,
                    Email = result.Value.Email,
                    Phone = result.Value.Phone,
                    Address = result.Value.Address
                },
                GetLocalReturnUrl(returnUrl)));
        }

        return IsSupplierNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("edit", result.Error, id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        [Bind(Prefix = nameof(SupplierEditPageViewModel.Input))] SupplierInputModel input,
        CancellationToken cancellationToken)
    {
        var page = new SupplierEditPageViewModel(
            id,
            input,
            GetLocalReturnUrl(returnUrl));

        if (!ModelState.IsValid)
        {
            return View(page);
        }

        var result = await supplierService.UpdateAsync(id, input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Tedarikçi başarıyla güncellendi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            AddValidationError(result.Error, nameof(SupplierEditPageViewModel.Input));
            return View(page);
        }

        return IsSupplierNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("update", result.Error, id);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsSupplierNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("delete_confirmation", result.Error, id);
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.DeleteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessMessageKey] = "Tedarikçi başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        if (IsSupplierNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (IsSupplierHasOrders(result.Error))
        {
            ModelState.AddModelError(string.Empty, result.Error!.Message);
            var supplierResult = await supplierService.GetByIdAsync(id, cancellationToken);

            if (supplierResult.IsSuccess && supplierResult.Value is not null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Delete), supplierResult.Value);
            }

            return IsSupplierNotFound(supplierResult.Error)
                ? NotFoundView()
                : UnexpectedFailure("delete_reload", supplierResult.Error, id);
        }

        return UnexpectedFailure("delete", result.Error, id);
    }

    private void AddValidationError(ServiceError error, string? prefix = null)
    {
        var key = error.Code switch
        {
            SupplierServiceErrorCodes.CompanyNameRequired
                or SupplierServiceErrorCodes.CompanyNameTooLong =>
                nameof(SupplierInputModel.CompanyName),
            SupplierServiceErrorCodes.EmailTooLong or SupplierServiceErrorCodes.EmailInvalid =>
                nameof(SupplierInputModel.Email),
            SupplierServiceErrorCodes.PhoneTooLong or SupplierServiceErrorCodes.PhoneInvalid =>
                nameof(SupplierInputModel.Phone),
            SupplierServiceErrorCodes.AddressTooLong => nameof(SupplierInputModel.Address),
            _ => string.Empty
        };

        var modelStateKey = string.IsNullOrEmpty(key) || string.IsNullOrEmpty(prefix)
            ? key
            : $"{prefix}.{key}";
        ModelState.AddModelError(modelStateKey, error.Message);
    }

    private string GetLocalReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Url.Content(returnUrl);
        }

        return Url.Action(nameof(Index)) ?? "/Suppliers";
    }

    private static bool IsSupplierNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == SupplierServiceErrorCodes.SupplierNotFound;
    }

    private static bool IsSupplierHasOrders(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.BusinessRule
            && error.Code == SupplierServiceErrorCodes.SupplierHasOrders;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? supplierId = null)
    {
        logger.LogError(
            "Supplier MVC operation {Operation} returned unexpected result {ErrorCode} for supplier {SupplierId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "supplier.unexpected_result",
            supplierId,
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
