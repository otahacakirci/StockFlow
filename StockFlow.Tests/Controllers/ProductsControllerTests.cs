using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Categories;
using StockFlow.Services.Common;
using StockFlow.Services.Products;
using StockFlow.ViewModels.Categories;
using StockFlow.ViewModels.Products;

namespace StockFlow.Tests.Controllers;

public sealed class ProductsControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndTokenAndComposesProductAndCategoryData()
    {
        var query = new ProductListQueryModel
        {
            SearchTerm = "pen",
            CategoryId = 4,
            LowStockOnly = true,
            SortOrder = ProductSortOrder.StockQuantityAscending,
            Page = 2,
            PageSize = 10
        };
        var list = new ProductListViewModel(
            [Product(id: 7)],
            "pen",
            4,
            true,
            ProductSortOrder.StockQuantityAscending,
            2,
            10,
            11,
            2);
        IReadOnlyList<CategorySelectionOptionViewModel> categories =
            [new CategorySelectionOptionViewModel(4, "Office")];
        var productService = new StubProductService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductListViewModel>.Success(list))
        };
        var categoryService = new StubCategoryService
        {
            SelectionHandler = _ => Task.FromResult(
                ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>.Success(categories))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(productService, categoryService);

        var actionResult = await controller.Index(query, cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductListPageViewModel>(viewResult.Model);
        Assert.Same(list, page.Products);
        Assert.Same(categories, page.Categories);
        Assert.Same(query, productService.ReceivedListQuery);
        Assert.Equal(cancellationTokenSource.Token, productService.ReceivedCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, categoryService.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Index_WhenServiceReturnsUnexpectedFailure_ReturnsSafeErrorView()
    {
        var productService = new StubProductService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductListViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    ProductServiceErrorCodes.InputRequired,
                    "Güvenli mesaj.")))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Index(
            new ProductListQueryModel(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Details_WhenProductDoesNotExist_ReturnsSafeNotFoundView()
    {
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Failure(ProductNotFoundError()))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Details(404, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task CreateGet_LoadsCategoryOptionsWithCancellationToken()
    {
        IReadOnlyList<CategorySelectionOptionViewModel> categories =
            [new CategorySelectionOptionViewModel(2, "Office")];
        var categoryService = new StubCategoryService
        {
            SelectionHandler = _ => Task.FromResult(
                ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>.Success(categories))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(new StubProductService(), categoryService);

        var actionResult = await controller.Create(cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(ProductsController.Create), viewResult.ViewName);
        var page = Assert.IsType<ProductCreatePageViewModel>(viewResult.Model);
        Assert.Same(categories, page.Categories);
        Assert.Equal(cancellationTokenSource.Token, categoryService.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Create_WhenModelStateIsInvalid_ReloadsCategoriesWithoutCallingProductService()
    {
        var productService = new StubProductService();
        var controller = CreateController(productService);
        var input = new ProductCreateInputModel();
        controller.ModelState.AddModelError("Input.Name", "Geçersiz.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductCreatePageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(0, productService.CreateCallCount);
    }

    [Fact]
    public async Task Create_WhenPriceBindingFails_DoesNotCallProductService()
    {
        var productService = new StubProductService();
        var controller = CreateController(productService);
        var input = ValidCreate();
        controller.ModelState.AddModelError(
            "Input.Price",
            "Fiyatı 19,34 biçiminde girin.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductCreatePageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(0, productService.CreateCallCount);
    }

    [Theory]
    [InlineData(ProductServiceErrorCodes.InputRequired, "")]
    [InlineData(ProductServiceErrorCodes.NameRequired, "Input.Name")]
    [InlineData(ProductServiceErrorCodes.SkuDuplicate, "Input.Sku")]
    [InlineData(ProductServiceErrorCodes.PriceInvalid, "Input.Price")]
    [InlineData(ProductServiceErrorCodes.StockQuantityInvalid, "Input.StockQuantity")]
    [InlineData(ProductServiceErrorCodes.MinimumStockQuantityInvalid, "Input.MinimumStockQuantity")]
    [InlineData(ProductServiceErrorCodes.CategoryInvalid, "Input.CategoryId")]
    public async Task Create_WhenServiceRejectsField_AddsErrorAndKeepsInput(
        string errorCode,
        string expectedKey)
    {
        var input = ValidCreate();
        var productService = new StubProductService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    errorCode,
                    "Alan geçersiz.")))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductCreatePageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal("Alan geçersiz.", Assert.Single(controller.ModelState[expectedKey]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Create_WhenSelectedCategoryDisappears_AddsCategoryFieldError()
    {
        var productService = new StubProductService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.NotFound,
                    ProductServiceErrorCodes.CategoryNotFound,
                    "Kategori bulunamadı.")))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Create(ValidCreate(), CancellationToken.None);

        Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(
            "Kategori bulunamadı.",
            Assert.Single(controller.ModelState["Input.CategoryId"]!.Errors).ErrorMessage);
        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenSuccessful_RedirectsToDetailsAndSetsSuccessMessage()
    {
        var created = Product(id: 17);
        var productService = new StubProductService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(created))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(productService);
        var input = ValidCreate();
        input.Price = 19.34m;

        var actionResult = await controller.Create(input, cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(ProductsController.Details), redirect.ActionName);
        Assert.Equal(created.Id, redirect.RouteValues?["id"]);
        Assert.Same(input, productService.ReceivedCreateInput);
        Assert.Equal(
            19.34m,
            Assert.IsType<ProductCreateInputModel>(productService.ReceivedCreateInput).Price);
        Assert.Equal(cancellationTokenSource.Token, productService.ReceivedCancellationToken);
        Assert.Equal("Ürün başarıyla oluşturuldu.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task EditGet_ComposesEditableFieldsAndReadOnlyCurrentStock()
    {
        var product = Product(id: 23, stockQuantity: 9);
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Edit(product.Id, null, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Equal(product.Id, page.Id);
        Assert.Equal(9, page.CurrentStockQuantity);
        Assert.Equal(product.Name, page.Input.Name);
        Assert.Equal(product.Sku, page.Input.Sku);
        Assert.Equal(product.Price, page.Input.Price);
        Assert.Equal(product.MinimumStockQuantity, page.Input.MinimumStockQuantity);
        Assert.Equal(product.CategoryId, page.Input.CategoryId);
        Assert.Equal("/Products", page.ReturnUrl);
    }

    [Theory]
    [InlineData("/Products?SearchTerm=kalem&CategoryId=4&LowStockOnly=True&SortOrder=PriceDescending&Page=3&PageSize=50")]
    [InlineData("/Products/Details/23")]
    public async Task EditGet_WithLocalReturnUrl_PreservesExactTarget(string returnUrl)
    {
        var product = Product(id: 23);
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Edit(product.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Equal(returnUrl, page.ReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/Products")]
    [InlineData("//example.com/Products")]
    public async Task EditGet_WithMissingOrExternalReturnUrl_FallsBackToIndex(string? returnUrl)
    {
        var product = Product(id: 23);
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Edit(product.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Products", page.ReturnUrl);
    }

    [Fact]
    public async Task Edit_WhenModelStateIsInvalid_ReloadsServerStockWithoutUpdating()
    {
        var product = Product(id: 29, stockQuantity: 12);
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);
        var input = ValidUpdate();
        controller.ModelState.AddModelError("Input.Name", "Geçersiz.");

        const string returnUrl =
            "/Products?SearchTerm=kalem&CategoryId=4&LowStockOnly=True&SortOrder=PriceDescending&Page=3&PageSize=50";

        var actionResult = await controller.Edit(
            product.Id,
            returnUrl,
            input,
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(12, page.CurrentStockQuantity);
        Assert.Equal(returnUrl, page.ReturnUrl);
        Assert.Equal(0, productService.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenPriceBindingFails_DoesNotCallProductServiceAndRejectsExternalReturnUrl()
    {
        var product = Product(id: 29, stockQuantity: 12);
        var productService = new StubProductService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);
        controller.ModelState.AddModelError(
            "Input.Price",
            "Fiyatı 19,34 biçiminde girin.");

        var actionResult = await controller.Edit(
            product.Id,
            "https://example.com/Products",
            ValidUpdate(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Products", page.ReturnUrl);
        Assert.Equal(0, productService.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenServiceRejectsPrice_PreservesLocalReturnUrl()
    {
        const string returnUrl =
            "/Products?SearchTerm=kalem&CategoryId=4&LowStockOnly=True&SortOrder=PriceDescending&Page=3&PageSize=50";
        var product = Product(id: 29, stockQuantity: 12);
        var productService = new StubProductService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    ProductServiceErrorCodes.PriceInvalid,
                    "Fiyat geçersiz."))),
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Edit(
            product.Id,
            returnUrl,
            ValidUpdate(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<ProductEditPageViewModel>(viewResult.Model);
        Assert.Equal(returnUrl, page.ReturnUrl);
        Assert.Equal(
            "Fiyat geçersiz.",
            Assert.Single(controller.ModelState["Input.Price"]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Edit_WhenSuccessful_ForwardsSafeInputAndRedirectsToDetails()
    {
        var updated = Product(id: 31);
        var productService = new StubProductService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(updated))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(productService);
        var input = ValidUpdate();
        input.Price = 19.34m;

        var actionResult = await controller.Edit(
            updated.Id,
            null,
            input,
            cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(ProductsController.Details), redirect.ActionName);
        Assert.Equal(updated.Id, redirect.RouteValues?["id"]);
        Assert.Equal(updated.Id, productService.ReceivedProductId);
        Assert.Same(input, productService.ReceivedUpdateInput);
        Assert.Equal(
            19.34m,
            Assert.IsType<ProductUpdateInputModel>(productService.ReceivedUpdateInput).Price);
        Assert.Equal(cancellationTokenSource.Token, productService.ReceivedCancellationToken);
        Assert.Equal("Ürün başarıyla güncellendi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Edit_WhenProductDoesNotExist_ReturnsSafeNotFoundView()
    {
        var productService = new StubProductService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Failure(ProductNotFoundError()))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.Edit(404, null, ValidUpdate(), CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenBusinessRuleFails_ReloadsConfirmationWithConflictStatus()
    {
        var product = Product(id: 41, canDelete: false);
        var productService = new StubProductService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Failure(new ServiceError(
                ServiceErrorCategory.BusinessRule,
                ProductServiceErrorCodes.ProductHasHistory,
                "Geçmişi bulunan ürün silinemez."))),
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<ProductViewModel>.Success(product))
        };
        var controller = CreateController(productService);

        var actionResult = await controller.DeleteConfirmed(product.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(ProductsController.Delete), viewResult.ViewName);
        Assert.Same(product, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.Equal(
            "Geçmişi bulunan ürün silinemez.",
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Delete_WhenSuccessful_RedirectsToIndexAndSetsSuccessMessage()
    {
        var productService = new StubProductService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Success())
        };
        var controller = CreateController(productService);

        var actionResult = await controller.DeleteConfirmed(47, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);
        Assert.Equal(47, productService.ReceivedProductId);
        Assert.Equal("Ürün başarıyla silindi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void Controller_UsesReadRoleBoundaryAndSecuresEveryWriteAction()
    {
        var controllerAuthorize = Assert.Single(
            typeof(ProductsController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(
            [AppRoles.Admin, AppRoles.Employee],
            SplitRoles(controllerAuthorize.Roles));

        var declaredActions = typeof(ProductsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var writeActions = declaredActions.Where(method =>
            method.Name is nameof(ProductsController.Create)
                or nameof(ProductsController.Edit)
                or nameof(ProductsController.Delete)
                or nameof(ProductsController.DeleteConfirmed));

        Assert.All(writeActions, method =>
        {
            var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal([AppRoles.Admin], SplitRoles(authorize.Roles));
        });

        var postActions = declaredActions
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();
        var getActions = declaredActions
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .ToList();
        Assert.Equal(5, getActions.Count);
        Assert.Equal(3, postActions.Count);
        Assert.Equal(declaredActions.Length, getActions.Count + postActions.Count);
        Assert.All(postActions, method =>
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));

        var deletePost = Assert.Single(postActions, method =>
            method.Name == nameof(ProductsController.DeleteConfirmed));
        Assert.Equal(
            nameof(ProductsController.Delete),
            deletePost.GetCustomAttribute<ActionNameAttribute>()?.Name);
    }

    private static ProductsController CreateController(
        IProductService productService,
        ICategoryService? categoryService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "product-trace"
        };
        var controller = new ProductsController(
            productService,
            categoryService ?? new StubCategoryService(),
            NullLogger<ProductsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper("/Products")
        };
        controller.TempData = new TempDataDictionary(
            httpContext,
            new StubTempDataProvider());
        return controller;
    }

    private static ProductViewModel Product(
        int id,
        int stockQuantity = 5,
        bool canDelete = true)
    {
        return new ProductViewModel(
            id,
            "Office Pen",
            "PEN-001",
            12.50m,
            stockQuantity,
            2,
            4,
            "Office",
            false,
            canDelete);
    }

    private static ProductCreateInputModel ValidCreate()
    {
        return new ProductCreateInputModel
        {
            Name = "Office Pen",
            Sku = "PEN-001",
            Price = 12.50m,
            StockQuantity = 5,
            MinimumStockQuantity = 2,
            CategoryId = 4
        };
    }

    private static ProductUpdateInputModel ValidUpdate()
    {
        return new ProductUpdateInputModel
        {
            Name = "Office Pen",
            Sku = "PEN-001",
            Price = 12.50m,
            MinimumStockQuantity = 2,
            CategoryId = 4
        };
    }

    private static ServiceError ProductNotFoundError()
    {
        return new ServiceError(
            ServiceErrorCategory.NotFound,
            ProductServiceErrorCodes.ProductNotFound,
            "Ürün bulunamadı.");
    }

    private static string[] SplitRoles(string? roles)
    {
        Assert.NotNull(roles);
        return roles.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed class StubProductService : IProductService
    {
        public Func<ProductListQueryModel?, CancellationToken, Task<ServiceResult<ProductListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<ProductViewModel>>>?
            GetByIdHandler
        { get; init; }

        public Func<ProductCreateInputModel?, CancellationToken, Task<ServiceResult<ProductViewModel>>>?
            CreateHandler
        { get; init; }

        public Func<int, ProductUpdateInputModel?, CancellationToken, Task<ServiceResult<ProductViewModel>>>?
            UpdateHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult>>?
            DeleteHandler
        { get; init; }

        public int CreateCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public int? ReceivedProductId { get; private set; }

        public ProductCreateInputModel? ReceivedCreateInput { get; private set; }

        public ProductUpdateInputModel? ReceivedUpdateInput { get; private set; }

        public ProductListQueryModel? ReceivedListQuery { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<ProductListViewModel>> GetListAsync(
            ProductListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedListQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<ProductViewModel>> GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            ReceivedProductId = productId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(productId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<ProductViewModel>> CreateAsync(
            ProductCreateInputModel? input,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            ReceivedCreateInput = input;
            ReceivedCancellationToken = cancellationToken;
            return CreateHandler is not null
                ? CreateHandler(input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<ProductViewModel>> UpdateAsync(
            int productId,
            ProductUpdateInputModel? input,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            ReceivedProductId = productId;
            ReceivedUpdateInput = input;
            ReceivedCancellationToken = cancellationToken;
            return UpdateHandler is not null
                ? UpdateHandler(productId, input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            ReceivedProductId = productId;
            ReceivedCancellationToken = cancellationToken;
            return DeleteHandler is not null
                ? DeleteHandler(productId, cancellationToken)
                : throw MissingHandlerException();
        }
    }

    private sealed class StubCategoryService : ICategoryService
    {
        public Func<CancellationToken, Task<ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>>>?
            SelectionHandler
        { get; init; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return SelectionHandler is not null
                ? SelectionHandler(cancellationToken)
                : Task.FromResult(
                    ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>.Success([]));
        }

        public Task<ServiceResult<CategoryListViewModel>> GetListAsync(
            CategoryListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> GetByIdAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> CreateAsync(
            CategoryInputModel? input,
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> UpdateAsync(
            int categoryId,
            CategoryInputModel? input,
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }
    }

    private static InvalidOperationException MissingHandlerException()
    {
        return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}
