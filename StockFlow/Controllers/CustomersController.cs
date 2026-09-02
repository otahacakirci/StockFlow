using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class CustomersController(
    ICustomerService customerService,
    ILogger<CustomersController> logger) : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";

    [HttpGet]
    public async Task<IActionResult> Index(
        CustomerListQueryModel query,
        CancellationToken cancellationToken)
    {
        var result = await customerService.GetListAsync(query, cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? View(result.Value)
            : UnexpectedFailure("list", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await customerService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsCustomerNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CustomerInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await customerService.CreateAsync(input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Müşteri başarıyla oluşturuldu.";
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
        var result = await customerService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(new CustomerEditPageViewModel(
                result.Value.Id,
                new CustomerInputModel
                {
                    Name = result.Value.Name,
                    Email = result.Value.Email,
                    Phone = result.Value.Phone,
                    Address = result.Value.Address
                },
                GetLocalReturnUrl(returnUrl)));
        }

        return IsCustomerNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("edit", result.Error, id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        [Bind(Prefix = nameof(CustomerEditPageViewModel.Input))] CustomerInputModel input,
        CancellationToken cancellationToken)
    {
        var page = new CustomerEditPageViewModel(
            id,
            input,
            GetLocalReturnUrl(returnUrl));

        if (!ModelState.IsValid)
        {
            return View(page);
        }

        var result = await customerService.UpdateAsync(id, input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Müşteri başarıyla güncellendi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            AddValidationError(result.Error, nameof(CustomerEditPageViewModel.Input));
            return View(page);
        }

        return IsCustomerNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("update", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await customerService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsCustomerNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("delete_confirmation", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await customerService.DeleteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessMessageKey] = "Müşteri başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        if (IsCustomerNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (IsCustomerHasOrders(result.Error))
        {
            ModelState.AddModelError(string.Empty, result.Error!.Message);
            var customerResult = await customerService.GetByIdAsync(id, cancellationToken);

            if (customerResult.IsSuccess && customerResult.Value is not null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Delete), customerResult.Value);
            }

            return IsCustomerNotFound(customerResult.Error)
                ? NotFoundView()
                : UnexpectedFailure("delete_reload", customerResult.Error, id);
        }

        return UnexpectedFailure("delete", result.Error, id);
    }

    private void AddValidationError(ServiceError error, string? prefix = null)
    {
        var key = error.Code switch
        {
            CustomerServiceErrorCodes.NameRequired or CustomerServiceErrorCodes.NameTooLong =>
                nameof(CustomerInputModel.Name),
            CustomerServiceErrorCodes.EmailTooLong or CustomerServiceErrorCodes.EmailInvalid =>
                nameof(CustomerInputModel.Email),
            CustomerServiceErrorCodes.PhoneTooLong or CustomerServiceErrorCodes.PhoneInvalid =>
                nameof(CustomerInputModel.Phone),
            CustomerServiceErrorCodes.AddressTooLong => nameof(CustomerInputModel.Address),
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

        return Url.Action(nameof(Index)) ?? "/Customers";
    }

    private static bool IsCustomerNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == CustomerServiceErrorCodes.CustomerNotFound;
    }

    private static bool IsCustomerHasOrders(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.BusinessRule
            && error.Code == CustomerServiceErrorCodes.CustomerHasOrders;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? customerId = null)
    {
        logger.LogError(
            "Customer MVC operation {Operation} returned unexpected result {ErrorCode} for customer {CustomerId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "customer.unexpected_result",
            customerId,
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
