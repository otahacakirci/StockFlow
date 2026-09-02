using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Security;
using StockFlow.Services.Categories;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Categories;

namespace StockFlow.Tests.Controllers;

public sealed class CategoriesControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndCancellationTokenAndReturnsListViewModel()
    {
        var query = new CategoryListQueryModel
        {
            SearchTerm = "office",
            SortOrder = CategorySortOrder.NameDescending,
            Page = 2,
            PageSize = 10
        };
        var list = new CategoryListViewModel(
            [new CategoryViewModel(7, "Office", 2)],
            "office",
            CategorySortOrder.NameDescending,
            2,
            10,
            11,
            2);
        var service = new StubCategoryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryListViewModel>.Success(list))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Index(query, cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(list, viewResult.Model);
        Assert.Same(query, service.ReceivedListQuery);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Details_WhenCategoryDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubCategoryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Failure(NotFoundError()))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Details(404, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenModelStateIsInvalid_DoesNotCallService()
    {
        var service = new StubCategoryService();
        var controller = CreateController(service);
        var input = new CategoryInputModel { Name = string.Empty };
        controller.ModelState.AddModelError(nameof(CategoryInputModel.Name), "Geçersiz.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(input, viewResult.Model);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Fact]
    public async Task Create_WhenServiceRejectsName_AddsFieldErrorAndKeepsInput()
    {
        var input = new CategoryInputModel { Name = "   " };
        var service = new StubCategoryService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    CategoryServiceErrorCodes.NameRequired,
                    "Kategori adı zorunludur.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(input, viewResult.Model);
        var modelState = Assert.Contains(
            nameof(CategoryInputModel.Name),
            controller.ModelState);
        Assert.NotNull(modelState);
        Assert.Equal("Kategori adı zorunludur.", Assert.Single(modelState.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Create_WhenSuccessful_RedirectsToDetailsAndSetsSuccessMessage()
    {
        var created = new CategoryViewModel(17, "Consumables", 0);
        var service = new StubCategoryService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Success(created))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Create(
            new CategoryInputModel { Name = "Consumables" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CategoriesController.Details), redirect.ActionName);
        Assert.Equal(created.Id, redirect.RouteValues?["id"]);
        Assert.Equal("Kategori başarıyla oluşturuldu.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData("/Categories?SearchTerm=ofis&SortOrder=NameDescending&Page=3&PageSize=50")]
    [InlineData("/Categories/Details/5")]
    public async Task EditGet_WithLocalReturnUrl_PreservesExactTarget(string returnUrl)
    {
        var category = new CategoryViewModel(5, "Office", 1);
        var service = new StubCategoryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Success(category))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(category.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CategoryEditPageViewModel>(viewResult.Model);
        Assert.Equal(category.Id, page.Id);
        Assert.Equal(category.Name, page.Input.Name);
        Assert.Equal(returnUrl, page.ReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/Categories")]
    [InlineData("//example.com/Categories")]
    public async Task EditGet_WithMissingOrExternalReturnUrl_FallsBackToIndex(string? returnUrl)
    {
        var category = new CategoryViewModel(5, "Office", 1);
        var service = new StubCategoryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Success(category))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(category.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CategoryEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Categories", page.ReturnUrl);
    }

    [Fact]
    public async Task Edit_WhenModelStateIsInvalid_PreservesLocalReturnUrlWithoutCallingService()
    {
        const string returnUrl =
            "/Categories?SearchTerm=ofis&SortOrder=NameDescending&Page=3&PageSize=50";
        var input = new CategoryInputModel { Name = string.Empty };
        var service = new StubCategoryService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("Input.Name", "Geçersiz.");

        var actionResult = await controller.Edit(
            5,
            returnUrl,
            input,
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CategoryEditPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(returnUrl, page.ReturnUrl);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenExternalReturnUrlIsPosted_ReplacesItWithIndexFallback()
    {
        var service = new StubCategoryService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("Input.Name", "Geçersiz.");

        var actionResult = await controller.Edit(
            5,
            "https://example.com/Categories",
            new CategoryInputModel { Name = string.Empty },
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CategoryEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Categories", page.ReturnUrl);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenServiceRejectsName_AddsFieldErrorAndKeepsInput()
    {
        var input = new CategoryInputModel { Name = new string('x', 101) };
        var service = new StubCategoryService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    CategoryServiceErrorCodes.NameTooLong,
                    "Kategori adı en fazla 100 karakter olabilir.")))
        };
        var controller = CreateController(service);

        const string returnUrl =
            "/Categories?SearchTerm=ofis&SortOrder=NameDescending&Page=3&PageSize=50";

        var actionResult = await controller.Edit(5, returnUrl, input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CategoryEditPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(returnUrl, page.ReturnUrl);
        var modelState = Assert.Contains(
            "Input.Name",
            controller.ModelState);
        Assert.NotNull(modelState);
        Assert.Equal(
            "Kategori adı en fazla 100 karakter olabilir.",
            Assert.Single(modelState.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Edit_WhenSuccessful_ForwardsIdAndRedirectsToDetails()
    {
        var input = new CategoryInputModel { Name = "Updated" };
        var updated = new CategoryViewModel(23, "Updated", 1);
        var service = new StubCategoryService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Success(updated))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Edit(
            updated.Id,
            null,
            input,
            cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CategoriesController.Details), redirect.ActionName);
        Assert.Equal(updated.Id, redirect.RouteValues?["id"]);
        Assert.Equal(updated.Id, service.ReceivedCategoryId);
        Assert.Same(input, service.ReceivedInput);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
        Assert.Equal("Kategori başarıyla güncellendi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Edit_WhenCategoryDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubCategoryService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Failure(NotFoundError()))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(
            404,
            null,
            new CategoryInputModel { Name = "Missing" },
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenBusinessRuleFails_ReloadsConfirmationWithConflictStatus()
    {
        var category = new CategoryViewModel(31, "Protected", 2);
        var service = new StubCategoryService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Failure(new ServiceError(
                ServiceErrorCategory.BusinessRule,
                CategoryServiceErrorCodes.CategoryHasProducts,
                "Bağlı ürünü bulunan kategori fiziksel olarak silinemez."))),
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CategoryViewModel>.Success(category))
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(category.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(CategoriesController.Delete), viewResult.ViewName);
        Assert.Same(category, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(
            "Bağlı ürünü bulunan kategori fiziksel olarak silinemez.",
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Delete_WhenSuccessful_RedirectsToIndexAndSetsSuccessMessage()
    {
        var service = new StubCategoryService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Success())
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(42, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CategoriesController.Index), redirect.ActionName);
        Assert.Equal(42, service.ReceivedCategoryId);
        Assert.Equal("Kategori başarıyla silindi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void Controller_UsesReadRoleBoundaryAndSecuresEveryWriteAction()
    {
        var controllerAuthorize = Assert.Single(
            typeof(CategoriesController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(
            [AppRoles.Admin, AppRoles.Employee],
            SplitRoles(controllerAuthorize.Roles));

        var declaredActions = typeof(CategoriesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var writeActions = declaredActions.Where(method =>
            method.Name is nameof(CategoriesController.Create)
                or nameof(CategoriesController.Edit)
                or nameof(CategoriesController.Delete)
                or nameof(CategoriesController.DeleteConfirmed));

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
            method.Name == nameof(CategoriesController.DeleteConfirmed));
        Assert.Equal(
            nameof(CategoriesController.Delete),
            deletePost.GetCustomAttribute<ActionNameAttribute>()?.Name);
    }

    private static CategoriesController CreateController(ICategoryService categoryService)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "category-trace"
        };
        var controller = new CategoriesController(
            categoryService,
            NullLogger<CategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper("/Categories")
        };
        controller.TempData = new TempDataDictionary(
            httpContext,
            new StubTempDataProvider());
        return controller;
    }

    private static string[] SplitRoles(string? roles)
    {
        Assert.NotNull(roles);
        return roles.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static ServiceError NotFoundError()
    {
        return new ServiceError(
            ServiceErrorCategory.NotFound,
            CategoryServiceErrorCodes.CategoryNotFound,
            "Kategori bulunamadı.");
    }

    private sealed class StubCategoryService : ICategoryService
    {
        public Func<CategoryListQueryModel?, CancellationToken, Task<ServiceResult<CategoryListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<CategoryViewModel>>>?
            GetByIdHandler
        { get; init; }

        public Func<CategoryInputModel?, CancellationToken, Task<ServiceResult<CategoryViewModel>>>?
            CreateHandler
        { get; init; }

        public Func<int, CategoryInputModel?, CancellationToken, Task<ServiceResult<CategoryViewModel>>>?
            UpdateHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult>>?
            DeleteHandler
        { get; init; }

        public int CreateCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public int? ReceivedCategoryId { get; private set; }

        public CategoryInputModel? ReceivedInput { get; private set; }

        public CategoryListQueryModel? ReceivedListQuery { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<CategoryListViewModel>> GetListAsync(
            CategoryListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedListQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> GetByIdAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCategoryId = categoryId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(categoryId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<IReadOnlyList<CategorySelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> CreateAsync(
            CategoryInputModel? input,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return CreateHandler is not null
                ? CreateHandler(input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<CategoryViewModel>> UpdateAsync(
            int categoryId,
            CategoryInputModel? input,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            ReceivedCategoryId = categoryId;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return UpdateHandler is not null
                ? UpdateHandler(categoryId, input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCategoryId = categoryId;
            ReceivedCancellationToken = cancellationToken;
            return DeleteHandler is not null
                ? DeleteHandler(categoryId, cancellationToken)
                : throw MissingHandlerException();
        }

        private static InvalidOperationException MissingHandlerException()
        {
            return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
        }
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
