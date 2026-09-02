using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Tests.Controllers;

public sealed class CustomersControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndCancellationTokenAndReturnsListViewModel()
    {
        var query = new CustomerListQueryModel
        {
            SearchTerm = "office",
            SortOrder = CustomerSortOrder.NameDescending,
            Page = 2,
            PageSize = 10
        };
        var list = new CustomerListViewModel(
            [new CustomerViewModel(7, "Office", "office@example.com", null, null, 2)],
            "office",
            CustomerSortOrder.NameDescending,
            2,
            10,
            11,
            2);
        var service = new StubCustomerService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerListViewModel>.Success(list))
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
        var service = new StubCustomerService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerListViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    CustomerServiceErrorCodes.InputRequired,
                    "Beklenmeyen sonuç.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Index(
            new CustomerListQueryModel(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Index_WhenServiceThrows_DoesNotSwallowException()
    {
        var service = new StubCustomerService
        {
            GetListHandler = (_, _) => throw new TestUnexpectedException()
        };
        var controller = CreateController(service);

        await Assert.ThrowsAsync<TestUnexpectedException>(() => controller.Index(
            new CustomerListQueryModel(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Details_WhenCustomerDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubCustomerService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Failure(NotFoundError()))
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
        var service = new StubCustomerService();
        var controller = CreateController(service);
        var input = new CustomerInputModel { Name = string.Empty };
        controller.ModelState.AddModelError(nameof(CustomerInputModel.Name), "Geçersiz.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(input, viewResult.Model);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Theory]
    [InlineData(CustomerServiceErrorCodes.InputRequired, "")]
    [InlineData(CustomerServiceErrorCodes.NameRequired, nameof(CustomerInputModel.Name))]
    [InlineData(CustomerServiceErrorCodes.NameTooLong, nameof(CustomerInputModel.Name))]
    [InlineData(CustomerServiceErrorCodes.EmailTooLong, nameof(CustomerInputModel.Email))]
    [InlineData(CustomerServiceErrorCodes.EmailInvalid, nameof(CustomerInputModel.Email))]
    [InlineData(CustomerServiceErrorCodes.PhoneTooLong, nameof(CustomerInputModel.Phone))]
    [InlineData(CustomerServiceErrorCodes.PhoneInvalid, nameof(CustomerInputModel.Phone))]
    [InlineData(CustomerServiceErrorCodes.AddressTooLong, nameof(CustomerInputModel.Address))]
    public async Task CreateAndEdit_WhenServiceRejectsField_AddErrorsToCorrectKeys(
        string errorCode,
        string expectedKey)
    {
        var error = new ServiceError(
            ServiceErrorCategory.Validation,
            errorCode,
            "Alan geçersiz.");
        var service = new StubCustomerService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Failure(error)),
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Failure(error))
        };
        var createController = CreateController(service);
        var editController = CreateController(service);
        var createInput = ValidInput();
        var editInput = ValidInput();

        var createResult = await createController.Create(createInput, CancellationToken.None);
        var editResult = await editController.Edit(
            5,
            "/Customers",
            editInput,
            CancellationToken.None);

        var createView = Assert.IsType<ViewResult>(createResult);
        Assert.Same(createInput, createView.Model);
        var createState = Assert.Contains(expectedKey, createController.ModelState);
        Assert.NotNull(createState);
        Assert.Equal("Alan geçersiz.", Assert.Single(createState.Errors).ErrorMessage);

        var editView = Assert.IsType<ViewResult>(editResult);
        var page = Assert.IsType<CustomerEditPageViewModel>(editView.Model);
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
        var created = Customer(id: 17, name: "New Customer");
        var service = new StubCustomerService
        {
            CreateHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Success(created))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Create(ValidInput(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CustomersController.Details), redirect.ActionName);
        Assert.Equal(created.Id, redirect.RouteValues?["id"]);
        Assert.Equal("Müşteri başarıyla oluşturuldu.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData("/Customers?SearchTerm=ofis&SortOrder=NameDescending&Page=3&PageSize=50")]
    [InlineData("/Customers/Details/5")]
    public async Task EditGet_WithLocalReturnUrl_PopulatesFieldsAndPreservesExactTarget(
        string returnUrl)
    {
        var customer = new CustomerViewModel(
            5,
            "Office",
            "office@example.com",
            "+90 555 100 0000",
            "Office address",
            1);
        var service = new StubCustomerService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Success(customer))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(customer.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CustomerEditPageViewModel>(viewResult.Model);
        Assert.Equal(customer.Id, page.Id);
        Assert.Equal(customer.Name, page.Input.Name);
        Assert.Equal(customer.Email, page.Input.Email);
        Assert.Equal(customer.Phone, page.Input.Phone);
        Assert.Equal(customer.Address, page.Input.Address);
        Assert.Equal(returnUrl, page.ReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/Customers")]
    [InlineData("//example.com/Customers")]
    public async Task EditGet_WithMissingOrExternalReturnUrl_FallsBackToIndex(string? returnUrl)
    {
        var customer = Customer(id: 5, name: "Office");
        var service = new StubCustomerService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Success(customer))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Edit(customer.Id, returnUrl, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CustomerEditPageViewModel>(viewResult.Model);
        Assert.Equal("/Customers", page.ReturnUrl);
    }

    [Fact]
    public async Task Edit_WhenModelStateIsInvalid_PreservesInputAndSafeReturnUrlWithoutCallingService()
    {
        const string returnUrl =
            "/Customers?SearchTerm=ofis&SortOrder=NameDescending&Page=3&PageSize=50";
        var input = new CustomerInputModel { Name = string.Empty };
        var service = new StubCustomerService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("Input.Name", "Geçersiz.");

        var actionResult = await controller.Edit(5, returnUrl, input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<CustomerEditPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Equal(returnUrl, page.ReturnUrl);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task Edit_WhenSuccessful_ForwardsIdAndRedirectsToDetails()
    {
        var input = ValidInput(name: "Updated");
        var updated = Customer(id: 23, name: "Updated", orderCount: 1);
        var service = new StubCustomerService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Success(updated))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Edit(
            updated.Id,
            null,
            input,
            cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CustomersController.Details), redirect.ActionName);
        Assert.Equal(updated.Id, redirect.RouteValues?["id"]);
        Assert.Equal(updated.Id, service.ReceivedCustomerId);
        Assert.Same(input, service.ReceivedInput);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
        Assert.Equal("Müşteri başarıyla güncellendi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Edit_WhenCustomerDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubCustomerService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Failure(NotFoundError()))
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
        var customer = Customer(id: 31, name: "Protected", orderCount: 2);
        var service = new StubCustomerService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Failure(new ServiceError(
                ServiceErrorCategory.BusinessRule,
                CustomerServiceErrorCodes.CustomerHasOrders,
                "Sipariş geçmişi bulunan müşteri fiziksel olarak silinemez."))),
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<CustomerViewModel>.Success(customer))
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(customer.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(CustomersController.Delete), viewResult.ViewName);
        Assert.Same(customer, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(
            "Sipariş geçmişi bulunan müşteri fiziksel olarak silinemez.",
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Delete_WhenSuccessful_RedirectsToIndexAndSetsSuccessMessage()
    {
        var service = new StubCustomerService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Success())
        };
        var controller = CreateController(service);

        var actionResult = await controller.DeleteConfirmed(42, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(CustomersController.Index), redirect.ActionName);
        Assert.Equal(42, service.ReceivedCustomerId);
        Assert.Equal("Müşteri başarıyla silindi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void Controller_AllowsCustomerWorkForBothRolesAndRestrictsDeleteToAdmin()
    {
        var controllerAuthorize = Assert.Single(
            typeof(CustomersController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(
            [AppRoles.Admin, AppRoles.Employee],
            SplitRoles(controllerAuthorize.Roles));

        var declaredActions = typeof(CustomersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var deleteActions = declaredActions.Where(method =>
            method.Name is nameof(CustomersController.Delete)
                or nameof(CustomersController.DeleteConfirmed));
        Assert.All(deleteActions, method =>
        {
            var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal([AppRoles.Admin], SplitRoles(authorize.Roles));
        });

        var nonDeleteActions = declaredActions.Where(method =>
            method.Name is not nameof(CustomersController.Delete)
                and not nameof(CustomersController.DeleteConfirmed));
        Assert.All(nonDeleteActions, method =>
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
            method.Name == nameof(CustomersController.DeleteConfirmed));
        Assert.Equal(
            nameof(CustomersController.Delete),
            deletePost.GetCustomAttribute<ActionNameAttribute>()?.Name);
    }

    private static CustomersController CreateController(ICustomerService customerService)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "customer-trace"
        };
        var controller = new CustomersController(
            customerService,
            NullLogger<CustomersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper("/Customers")
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

    private static CustomerInputModel ValidInput(string name = "Valid Customer")
    {
        return new CustomerInputModel
        {
            Name = name,
            Email = "valid.customer@example.com",
            Phone = "+90 555 123 4567",
            Address = "Valid address"
        };
    }

    private static CustomerViewModel Customer(
        int id,
        string name,
        int orderCount = 0)
    {
        return new CustomerViewModel(
            id,
            name,
            "customer@example.com",
            "+90 555 123 4567",
            "Customer address",
            orderCount);
    }

    private static ServiceError NotFoundError()
    {
        return new ServiceError(
            ServiceErrorCategory.NotFound,
            CustomerServiceErrorCodes.CustomerNotFound,
            "Müşteri bulunamadı.");
    }

    private sealed class StubCustomerService : ICustomerService
    {
        public Func<CustomerListQueryModel?, CancellationToken, Task<ServiceResult<CustomerListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<CustomerViewModel>>>?
            GetByIdHandler
        { get; init; }

        public Func<CustomerInputModel?, CancellationToken, Task<ServiceResult<CustomerViewModel>>>?
            CreateHandler
        { get; init; }

        public Func<int, CustomerInputModel?, CancellationToken, Task<ServiceResult<CustomerViewModel>>>?
            UpdateHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult>>?
            DeleteHandler
        { get; init; }

        public int CreateCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public int? ReceivedCustomerId { get; private set; }

        public CustomerInputModel? ReceivedInput { get; private set; }

        public CustomerListQueryModel? ReceivedListQuery { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<CustomerListViewModel>> GetListAsync(
            CustomerListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedListQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<CustomerViewModel>> GetByIdAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCustomerId = customerId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(customerId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            throw MissingHandlerException();
        }

        public Task<ServiceResult<CustomerViewModel>> CreateAsync(
            CustomerInputModel? input,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return CreateHandler is not null
                ? CreateHandler(input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<CustomerViewModel>> UpdateAsync(
            int customerId,
            CustomerInputModel? input,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            ReceivedCustomerId = customerId;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return UpdateHandler is not null
                ? UpdateHandler(customerId, input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCustomerId = customerId;
            ReceivedCancellationToken = cancellationToken;
            return DeleteHandler is not null
                ? DeleteHandler(customerId, cancellationToken)
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
