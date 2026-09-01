using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Categories;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class CategoriesController(
    ICategoryService categoryService,
    ILogger<CategoriesController> logger) : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";

    [HttpGet]
    public async Task<IActionResult> Index(
        CategoryListQueryModel query,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.GetListAsync(query, cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? View(result.Value)
            : UnexpectedFailure("list", result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CategoryInputModel());
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CategoryInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await categoryService.CreateAsync(input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Kategori başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            AddValidationError(result.Error);
            return View(input);
        }

        return UnexpectedFailure("create", result.Error);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(new CategoryInputModel { Name = result.Value.Name });
        }

        return IsNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("edit", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CategoryInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await categoryService.UpdateAsync(id, input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Kategori başarıyla güncellendi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (result.Error?.Category == ServiceErrorCategory.Validation)
        {
            AddValidationError(result.Error);
            return View(input);
        }

        return IsNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("update", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsNotFound(result.Error)
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
        var result = await categoryService.DeleteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessMessageKey] = "Kategori başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        if (IsNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (result.Error?.Category == ServiceErrorCategory.BusinessRule)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            var categoryResult = await categoryService.GetByIdAsync(id, cancellationToken);

            if (categoryResult.IsSuccess && categoryResult.Value is not null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Delete), categoryResult.Value);
            }

            return IsNotFound(categoryResult.Error)
                ? NotFoundView()
                : UnexpectedFailure("delete_reload", categoryResult.Error, id);
        }

        return UnexpectedFailure("delete", result.Error, id);
    }

    private void AddValidationError(ServiceError error)
    {
        var key = error.Code is CategoryServiceErrorCodes.NameRequired
            or CategoryServiceErrorCodes.NameTooLong
            ? nameof(CategoryInputModel.Name)
            : string.Empty;

        ModelState.AddModelError(key, error.Message);
    }

    private static bool IsNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? categoryId = null)
    {
        logger.LogError(
            "Category MVC operation {Operation} returned unexpected result {ErrorCode} for category {CategoryId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "category.unexpected_result",
            categoryId,
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
