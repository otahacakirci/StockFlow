using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Suppliers;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Tests.Controllers;

public sealed class SuppliersControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndCancellationTokenAndReturnsListViewModel()
    {
        var query = new SupplierListQueryModel
        {
            SearchTerm = "office",
            SortOrder = SupplierSortOrder.CompanyNameDescending,
            Page = 2,
            PageSize = 10
        };
        var list = new SupplierListViewModel(
            [new SupplierViewModel(7, "Office Supply", "office@example.com", null, null, 2)],
            "office",
            SupplierSortOrder.CompanyNameDescending,
            2,
            10,
            11,
            2);
        var service = new StubSupplierService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierListViewModel>.Success(list))
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
    public async Task Index_WhenServiceReturnsUnexpectedFailure_ReturnsSafeErrorView()
    {
        var service = new StubSupplierService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierListViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    SupplierServiceErrorCodes.InputRequired,
                    "Beklenmeyen sonuç.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Index(
            new SupplierListQueryModel(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Index_WhenServiceThrows_DoesNotSwallowException()
    {
        var service = new StubSupplierService
        {
            GetListHandler = (_, _) => throw new TestUnexpectedException()
        };
        var controller = CreateController(service);

        await Assert.ThrowsAsync<TestUnexpectedException>(() => controller.Index(
            new SupplierListQueryModel(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Details_WhenSupplierDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubSupplierService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Failure(NotFoundError()))
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
        var service = new StubSupplierService();
        var controller = CreateController(service);
        var input = new SupplierInputModel { CompanyName = string.Empty };
        controller.ModelState.AddModelError(nameof(SupplierInputModel.CompanyName), "Geçersiz.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(input, viewResult.Model);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Theory]
    [InlineData(SupplierServiceErrorCodes.InputRequired, "")]
    [InlineData(SupplierServiceErrorCodes.CompanyNameRequired, nameof(SupplierInputModel.CompanyName))]
    [InlineData(SupplierServiceErrorCodes.CompanyNameTooLong, nameof(SupplierInputModel.CompanyName))]
    [InlineData(SupplierServiceErrorCodes.EmailTooLong, nameof(SupplierInputModel.Email))]
    [InlineData(SupplierServiceErrorCodes.EmailInvalid, nameof(SupplierInputModel.Email))]
    [InlineData(SupplierServiceErrorCodes.PhoneTooLong, nameof(SupplierInputModel.Phone))]
    [InlineData(SupplierServiceErrorCodes.PhoneInvalid, nameof(SupplierInputModel.Phone))]
    [InlineData(SupplierServiceErrorCodes.AddressTooLong, nameof(SupplierInputModel.Address))]
    public async Task CreateAndEdit_WhenServiceRejectsField_AddErrorsToCorrectKeys(
        string errorCode,
        string expectedKey)
    {
        var error = new ServiceError(
            ServiceErrorCategory.Validation,
            errorCode,
            "Alan geçersiz.");
        var service = new StubSupplierService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Failure(error)),
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Failure(error))
        };
        var createController = CreateController(service);
        var editController = CreateController(service);
        var createInput = ValidInput();
        var editInput = ValidInput();

        var createResult = await createController.Create(createInput, CancellationToken.None);
        var editResult = await editController.Edit(
            5,
            "/Suppliers",
            editInput,
            CancellationToken.None);

        var createView = Assert.IsType<ViewResult>(createResult);
        Assert.Same(createInput, createView.Model);
        var createState = Assert.Contains(expectedKey, createController.ModelState);
        Assert.NotNull(createState);
        Assert.Equal("Alan geçersiz.", Assert.Single(createState.Errors).ErrorMessage);

        var editView = Assert.IsType<ViewResult>(editResult);
        var page = Assert.IsType<SupplierEditPageViewModel>(editView.Model);
        Assert.Same(editInput, page.Input);
        var editState = Assert.Contains(
            string.IsNullOrEmpty(expectedKey) ? string.Empty : $"Input.{expectedKey}",
            editController.ModelState);
        Assert.NotNull(editState);
        Assert.Equal("Alan geçersiz.", Assert.Single(editState.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Create_WhenSuccessful_RedirectsToDetailsAndSetsSuccessMessage()
    {
        var created = Supplier(id: 17, companyName: "New Supplier");
        var service = new StubSupplierService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Success(created))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Create(ValidInput(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(SuppliersController.Details), redirect.ActionName);
        Assert.Equal(created.Id, redirect.RouteValues?["id"]);
        Assert.Equal("Tedarikçi başarıyla oluşturuldu.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData("/Suppliers?SearchTerm=ofis&SortOrder=CompanyNameDescending&Page=3&PageSize=50")]
    [InlineData("/Suppliers/Details/5")]
    public async Task EditGet_WithLocalReturnUrl_PopulatesFieldsAndPreservesExactTarget(
        string returnUrl)
    {
        var supplier = new SupplierViewModel(
            5,
            "Office Supply",
            "office@example.com",
            "+90 555 100 0000",
            "Office address",
            1);
        var service = new StubSupplierService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Success(supplier))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(supplier.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<SupplierEditPageViewModel>(viewResult.Model);
        Assert.Equal(supplier.Id, page.Id);
        Assert.Equal(supplier.CompanyName, page.Input.CompanyName);
        Assert.Equal(supplier.Email, page.Input.Email);
        Assert.Equal(supplier.Phone, page.Input.Phone);
        Assert.Equal(supplier.Address, page.Input.Address);
        Assert.Equal(returnUrl, page.ReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/Suppliers")]
    [InlineData("//example.com/Suppliers")]
    public async Task EditGet_WithMissingOrExternalReturnUrl_FallsBackToIndex(string? returnUrl)
    {
        var supplier = Supplier(id: 5, companyName: "Office Supply");
        var service = new StubSupplierService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Success(supplier))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(supplier.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<SupplierEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Suppliers", page.ReturnUrl);
    }

    [Fact]
    public async Task Edit_WhenModelStateIsInvalid_PreservesInputAndSafeReturnUrlWithoutCallingService()
    {
        const string returnUrl =
            "/Suppliers?SearchTerm=ofis&SortOrder=CompanyNameDescending&Page=3&PageSize=50";
        var input = new SupplierInputModel { CompanyName = string.Empty };
        var service = new StubSupplierService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("Input.CompanyName", "Geçersiz.");

        var actionResult = await controller.Edit(5, returnUrl, input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<SupplierEditPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(returnUrl, page.ReturnUrl);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenSuccessful_ForwardsIdAndRedirectsToDetails()
    {
        var input = ValidInput(companyName: "Updated Supply");
        var updated = Supplier(id: 23, companyName: "Updated Supply", orderCount: 1);
        var service = new StubSupplierService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Success(updated))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Edit(
            updated.Id,
            null,
            input,
            cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(SuppliersController.Details), redirect.ActionName);
        Assert.Equal(updated.Id, redirect.RouteValues?["id"]);
        Assert.Equal(updated.Id, service.ReceivedSupplierId);
        Assert.Same(input, service.ReceivedInput);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
        Assert.Equal("Tedarikçi başarıyla güncellendi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Edit_WhenSupplierDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubSupplierService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Failure(NotFoundError()))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(
            404,
            null,
            ValidInput(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenBusinessRuleFails_ReloadsConfirmationWithConflictStatus()
    {
        var supplier = Supplier(id: 31, companyName: "Protected Supply", orderCount: 2);
        var service = new StubSupplierService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Failure(new ServiceError(
                ServiceErrorCategory.BusinessRule,
                SupplierServiceErrorCodes.SupplierHasOrders,
                "Sipariş geçmişi bulunan tedarikçi fiziksel olarak silinemez."))),
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<SupplierViewModel>.Success(supplier))
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(supplier.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(SuppliersController.Delete), viewResult.ViewName);
        Assert.Same(supplier, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(
            "Sipariş geçmişi bulunan tedarikçi fiziksel olarak silinemez.",
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Delete_WhenSuccessful_RedirectsToIndexAndSetsSuccessMessage()
    {
        var service = new StubSupplierService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Success())
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(42, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(SuppliersController.Index), redirect.ActionName);
        Assert.Equal(42, service.ReceivedSupplierId);
        Assert.Equal("Tedarikçi başarıyla silindi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void Controller_RequiresAdminForEveryActionAndSecuresEveryPost()
    {
        var controllerAuthorize = Assert.Single(
            typeof(SuppliersController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal([AppRoles.Admin], SplitRoles(controllerAuthorize.Roles));

        var declaredActions = typeof(SuppliersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.All(declaredActions, method =>
            Assert.Empty(method.GetCustomAttributes<AuthorizeAttribute>()));

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
            method.Name == nameof(SuppliersController.DeleteConfirmed));
        Assert.Equal(
            nameof(SuppliersController.Delete),
            deletePost.GetCustomAttribute<ActionNameAttribute>()?.Name);
    }

    [Fact]
    public async Task ControllerAuthorizationPolicy_AllowsAdminAndRejectsEmployee()
    {
        var authorize = Assert.Single(
            typeof(SuppliersController).GetCustomAttributes<AuthorizeAttribute>());
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(SplitRoles(authorize.Roles))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        using var serviceProvider = services.BuildServiceProvider();
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();

        var adminResult = await authorizationService.AuthorizeAsync(
            CreatePrincipal(AppRoles.Admin),
            resource: null,
            policy);
        var employeeResult = await authorizationService.AuthorizeAsync(
            CreatePrincipal(AppRoles.Employee),
            resource: null,
            policy);

        Assert.True(adminResult.Succeeded);
        Assert.False(employeeResult.Succeeded);
    }

    private static SuppliersController CreateController(ISupplierService supplierService)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "supplier-trace"
        };
        var controller = new SuppliersController(
            supplierService,
            NullLogger<SuppliersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper("/Suppliers")
        };
        controller.TempData = new TempDataDictionary(
            httpContext,
            new StubTempDataProvider());
        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, $"{role}-user"),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "Test"));
    }

    private static string[] SplitRoles(string? roles)
    {
        Assert.NotNull(roles);
        return roles.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static SupplierInputModel ValidInput(string companyName = "Valid Supplier")
    {
        return new SupplierInputModel
        {
            CompanyName = companyName,
            Email = "valid.supplier@example.com",
            Phone = "+90 555 123 4567",
            Address = "Valid address"
        };
    }

    private static SupplierViewModel Supplier(
        int id,
        string companyName,
        int orderCount = 0)
    {
        return new SupplierViewModel(
            id,
            companyName,
            "supplier@example.com",
            "+90 555 123 4567",
            "Supplier address",
            orderCount);
    }

    private static ServiceError NotFoundError()
    {
        return new ServiceError(
            ServiceErrorCategory.NotFound,
            SupplierServiceErrorCodes.SupplierNotFound,
            "Tedarikçi bulunamadı.");
    }

    private sealed class StubSupplierService : ISupplierService
    {
        public Func<SupplierListQueryModel?, CancellationToken, Task<ServiceResult<SupplierListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<SupplierViewModel>>>?
            GetByIdHandler
        { get; init; }

        public Func<SupplierInputModel?, CancellationToken, Task<ServiceResult<SupplierViewModel>>>?
            CreateHandler
        { get; init; }

        public Func<int, SupplierInputModel?, CancellationToken, Task<ServiceResult<SupplierViewModel>>>?
            UpdateHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult>>?
            DeleteHandler
        { get; init; }

        public int CreateCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public int? ReceivedSupplierId { get; private set; }

        public SupplierInputModel? ReceivedInput { get; private set; }

        public SupplierListQueryModel? ReceivedListQuery { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<SupplierListViewModel>> GetListAsync(
            SupplierListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedListQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<SupplierViewModel>> GetByIdAsync(
            int supplierId,
            CancellationToken cancellationToken = default)
        {
            ReceivedSupplierId = supplierId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(supplierId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<SupplierViewModel>> CreateAsync(
            SupplierInputModel? input,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return CreateHandler is not null
                ? CreateHandler(input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<SupplierViewModel>> UpdateAsync(
            int supplierId,
            SupplierInputModel? input,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            ReceivedSupplierId = supplierId;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return UpdateHandler is not null
                ? UpdateHandler(supplierId, input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteAsync(
            int supplierId,
            CancellationToken cancellationToken = default)
        {
            ReceivedSupplierId = supplierId;
            ReceivedCancellationToken = cancellationToken;
            return DeleteHandler is not null
                ? DeleteHandler(supplierId, cancellationToken)
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

    private sealed class TestUnexpectedException : Exception;
}
