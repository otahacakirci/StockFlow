using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Categories;
using StockFlow.Services.Common;
using StockFlow.Services.Products;
using StockFlow.ViewModels.Products;

namespace StockFlow.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
public sealed class ProductsController(
    IProductService productService,
    ICategoryService categoryService,
    ILogger<ProductsController> logger) : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";

    [HttpGet]
    public async Task<IActionResult> Index(
        ProductListQueryModel query,
        CancellationToken cancellationToken)
    {
        var productResult = await productService.GetListAsync(query, cancellationToken);
        if (!productResult.IsSuccess || productResult.Value is null)
        {
            return UnexpectedFailure("list", productResult.Error);
        }

        var categoryResult = await categoryService.GetSelectionOptionsAsync(cancellationToken);
        if (!categoryResult.IsSuccess || categoryResult.Value is null)
        {
            return UnexpectedFailure("list_categories", categoryResult.Error);
        }

        return View(new ProductListPageViewModel(
            productResult.Value,
            categoryResult.Value));
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsProductNotFound(result.Error)
            ? NotFoundView()
            : UnexpectedFailure("details", result.Error, id);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return await CreateFormViewAsync(
            new ProductCreateInputModel(),
            cancellationToken,
            "create_categories");
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = nameof(ProductCreatePageViewModel.Input))] ProductCreateInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await CreateFormViewAsync(
                input,
                cancellationToken,
                "create_validation_categories");
        }

        var result = await productService.CreateAsync(input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Ürün başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (IsFormError(result.Error))
        {
            AddValidationError(result.Error!);
            return await CreateFormViewAsync(
                input,
                cancellationToken,
                "create_error_categories");
        }

        return UnexpectedFailure("create", result.Error);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return IsProductNotFound(result.Error)
                ? NotFoundView()
                : UnexpectedFailure("edit", result.Error, id);
        }

        var input = new ProductUpdateInputModel
        {
            Name = result.Value.Name,
            Sku = result.Value.Sku,
            Price = result.Value.Price,
            MinimumStockQuantity = result.Value.MinimumStockQuantity,
            CategoryId = result.Value.CategoryId
        };

        return await EditFormViewAsync(
            result.Value,
            input,
            GetLocalReturnUrl(returnUrl),
            cancellationToken,
            "edit_categories");
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        string? returnUrl,
        [Bind(Prefix = nameof(ProductEditPageViewModel.Input))] ProductUpdateInputModel input,
        CancellationToken cancellationToken)
    {
        var safeReturnUrl = GetLocalReturnUrl(returnUrl);

        if (!ModelState.IsValid)
        {
            return await ReloadEditFormAsync(
                id,
                input,
                safeReturnUrl,
                cancellationToken,
                "edit_validation");
        }

        var result = await productService.UpdateAsync(id, input, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[SuccessMessageKey] = "Ürün başarıyla güncellendi.";
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        if (IsProductNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (IsFormError(result.Error))
        {
            AddValidationError(result.Error!);
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
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return View(result.Value);
        }

        return IsProductNotFound(result.Error)
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
        var result = await productService.DeleteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessMessageKey] = "Ürün başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        if (IsProductNotFound(result.Error))
        {
            return NotFoundView();
        }

        if (result.Error?.Category == ServiceErrorCategory.BusinessRule)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            var productResult = await productService.GetByIdAsync(id, cancellationToken);

            if (productResult.IsSuccess && productResult.Value is not null)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Delete), productResult.Value);
            }

            return IsProductNotFound(productResult.Error)
                ? NotFoundView()
                : UnexpectedFailure("delete_reload", productResult.Error, id);
        }

        return UnexpectedFailure("delete", result.Error, id);
    }

    private async Task<IActionResult> CreateFormViewAsync(
        ProductCreateInputModel input,
        CancellationToken cancellationToken,
        string operation)
    {
        var categoryResult = await categoryService.GetSelectionOptionsAsync(cancellationToken);

        return categoryResult.IsSuccess && categoryResult.Value is not null
            ? View(nameof(Create), new ProductCreatePageViewModel(input, categoryResult.Value))
            : UnexpectedFailure(operation, categoryResult.Error);
    }

    private async Task<IActionResult> ReloadEditFormAsync(
        int productId,
        ProductUpdateInputModel input,
        string returnUrl,
        CancellationToken cancellationToken,
        string operation)
    {
        var productResult = await productService.GetByIdAsync(productId, cancellationToken);
        if (!productResult.IsSuccess || productResult.Value is null)
        {
            return IsProductNotFound(productResult.Error)
                ? NotFoundView()
                : UnexpectedFailure(operation + "_product", productResult.Error, productId);
        }

        return await EditFormViewAsync(
            productResult.Value,
            input,
            returnUrl,
            cancellationToken,
            operation + "_categories");
    }

    private async Task<IActionResult> EditFormViewAsync(
        ProductViewModel product,
        ProductUpdateInputModel input,
        string returnUrl,
        CancellationToken cancellationToken,
        string operation)
    {
        var categoryResult = await categoryService.GetSelectionOptionsAsync(cancellationToken);
        if (!categoryResult.IsSuccess || categoryResult.Value is null)
        {
            return UnexpectedFailure(operation, categoryResult.Error, product.Id);
        }

        return View(nameof(Edit), new ProductEditPageViewModel(
            product.Id,
            product.StockQuantity,
            input,
            categoryResult.Value,
            returnUrl));
    }

    private string GetLocalReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Url.Content(returnUrl);
        }

        return Url.Action(nameof(Index)) ?? "/Products";
    }

    private void AddValidationError(ServiceError error)
    {
        var key = error.Code switch
        {
            ProductServiceErrorCodes.NameRequired or ProductServiceErrorCodes.NameTooLong =>
                nameof(ProductCreateInputModel.Name),
            ProductServiceErrorCodes.SkuRequired
                or ProductServiceErrorCodes.SkuTooLong
                or ProductServiceErrorCodes.SkuDuplicate => nameof(ProductCreateInputModel.Sku),
            ProductServiceErrorCodes.PriceInvalid => nameof(ProductCreateInputModel.Price),
            ProductServiceErrorCodes.StockQuantityInvalid => nameof(ProductCreateInputModel.StockQuantity),
            ProductServiceErrorCodes.MinimumStockQuantityInvalid =>
                nameof(ProductCreateInputModel.MinimumStockQuantity),
            ProductServiceErrorCodes.CategoryInvalid or ProductServiceErrorCodes.CategoryNotFound =>
                nameof(ProductCreateInputModel.CategoryId),
            _ => string.Empty
        };

        var modelStateKey = string.IsNullOrEmpty(key)
            ? string.Empty
            : $"{nameof(ProductCreatePageViewModel.Input)}.{key}";
        ModelState.AddModelError(modelStateKey, error.Message);
    }

    private static bool IsFormError(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.Validation
            || error?.Code == ProductServiceErrorCodes.CategoryNotFound;
    }

    private static bool IsProductNotFound(ServiceError? error)
    {
        return error?.Category == ServiceErrorCategory.NotFound
            && error.Code == ProductServiceErrorCodes.ProductNotFound;
    }

    private IActionResult NotFoundView()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private IActionResult UnexpectedFailure(
        string operation,
        ServiceError? error,
        int? productId = null)
    {
        logger.LogError(
            "Product MVC operation {Operation} returned unexpected result {ErrorCode} for product {ProductId}. TraceIdentifier: {TraceIdentifier}",
            operation,
            error?.Code ?? "product.unexpected_result",
            productId,
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
