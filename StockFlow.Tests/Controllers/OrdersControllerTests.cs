using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.Services.Suppliers;
using StockFlow.ViewModels.Customers;
using StockFlow.ViewModels.Orders;
using StockFlow.ViewModels.Products;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Tests.Controllers;

public sealed class OrdersControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndCancellationTokenAndReturnsListViewModel()
    {
        var query = new OrderListQueryModel
        {
            Type = OrderType.Purchase,
            Status = OrderStatus.Confirmed,
            SortOrder = OrderSortOrder.DateAscending,
            Page = 2,
            PageSize = 10
        };
        var list = new OrderListViewModel(
            [new OrderListItemViewModel(
                17,
                "ORDER-17",
                OrderType.Purchase,
                OrderStatus.Confirmed,
                UtcDate,
                1250m,
                "Test Supplier",
                2)],
            OrderType.Purchase,
            OrderStatus.Confirmed,
            OrderSortOrder.DateAscending,
            2,
            10,
            11,
            2);
        var service = new StubOrderQueryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderListViewModel>.Success(list))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Index(query, cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(list, viewResult.Model);
        Assert.Same(query, service.ReceivedListQuery);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData(ServiceErrorCategory.Validation, OrderServiceErrorCodes.InvalidOrderType)]
    [InlineData(ServiceErrorCategory.BusinessRule, OrderServiceErrorCodes.OrderNotDraft)]
    [InlineData(ServiceErrorCategory.NotFound, OrderServiceErrorCodes.OrderNotFound)]
    public async Task Index_WhenServiceReturnsUnexpectedFailure_ReturnsSafeErrorView(
        ServiceErrorCategory category,
        string errorCode)
    {
        var service = new StubOrderQueryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderListViewModel>.Failure(new ServiceError(
                    category,
                    errorCode,
                    "Kullanıcıya taşınmaması gereken ayrıntı.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Index(
            new OrderListQueryModel(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Index_WhenServiceThrows_DoesNotSwallowException()
    {
        var service = new StubOrderQueryService
        {
            GetListHandler = (_, _) => throw new TestUnexpectedException()
        };
        var controller = CreateController(service);

        await Assert.ThrowsAsync<TestUnexpectedException>(() => controller.Index(
            new OrderListQueryModel(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Details_ForwardsIdAndCancellationTokenAndReturnsDetailViewModel()
    {
        var detail = CreateDetail();
        var service = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Details(detail.Id, cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(detail.Id, service.ReceivedOrderId);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Details_WhenOrderDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.NotFound,
                    OrderServiceErrorCodes.OrderNotFound,
                    "Sipariş bulunamadı.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Details(404, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(ServiceErrorCategory.Validation, OrderServiceErrorCodes.InvalidParty)]
    [InlineData(ServiceErrorCategory.BusinessRule, OrderServiceErrorCodes.OrderNotDraft)]
    [InlineData(ServiceErrorCategory.NotFound, ProductServiceErrorCodes.ProductNotFound)]
    public async Task Details_WhenServiceReturnsNonOrderNotFoundFailure_ReturnsSafeErrorView(
        ServiceErrorCategory category,
        string errorCode)
    {
        var service = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Failure(new ServiceError(
                    category,
                    errorCode,
                    "Kullanıcıya taşınmaması gereken ayrıntı.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Details(17, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task CreateGet_ComposesPurchaseFormWithPartyAndProductSelections()
    {
        var controller = CreateController(new StubOrderQueryService());

        var actionResult = await controller.Create(OrderType.Purchase, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Create), viewResult.ViewName);
        var page = Assert.IsType<OrderDraftFormPageViewModel>(viewResult.Model);
        Assert.Equal(OrderType.Purchase, page.Input.Type);
        Assert.Equal(1, Assert.Single(page.Input.Items).Quantity);
        Assert.Equal("Test Customer", Assert.Single(page.Customers).Name);
        Assert.Equal("Test Supplier", Assert.Single(page.Suppliers).CompanyName);
        Assert.Equal("TEST-SKU", Assert.Single(page.Products).Sku);
        Assert.Null(page.OrderId);
    }

    [Fact]
    public async Task CreatePost_ForwardsSafeInputUserAndTokenAndRedirectsToDetails()
    {
        var input = ValidSaleInput();
        var orderService = new StubOrderService
        {
            CreateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Success(new OrderMutationResult(
                    41,
                    "ORDER-41",
                    OrderStatus.Draft,
                    25m)))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.Create(input, cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(41, redirect.RouteValues!["id"]);
        Assert.Same(input, orderService.ReceivedInput);
        Assert.Equal($"{AppRoles.Admin}-user", orderService.ReceivedUserId);
        Assert.Equal(cancellationTokenSource.Token, orderService.ReceivedCancellationToken);
        Assert.Equal("Taslak sipariş başarıyla oluşturuldu.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task CreatePost_WhenModelStateIsInvalid_PreservesInputAndReloadsSelections()
    {
        var input = ValidSaleInput();
        var orderService = new StubOrderService();
        var controller = CreateController(new StubOrderQueryService(), orderService);
        controller.ModelState.AddModelError(
            $"{nameof(OrderDraftFormPageViewModel.Input)}.{nameof(OrderDraftInputModel.Items)}",
            "Test doğrulama hatası.");

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<OrderDraftFormPageViewModel>(viewResult.Model);
        Assert.Same(input, page.Input);
        Assert.Single(page.Customers);
        Assert.Single(page.Suppliers);
        Assert.Single(page.Products);
        Assert.Equal(0, orderService.CreateCallCount);
    }

    [Fact]
    public async Task CreatePost_WhenSelectedProductDisappears_ShowsServiceErrorOnReloadedForm()
    {
        var input = ValidSaleInput();
        var orderService = new StubOrderService
        {
            CreateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Failure(new ServiceError(
                    ServiceErrorCategory.NotFound,
                    ProductServiceErrorCodes.ProductNotFound,
                    "Sipariş kalemlerinden en az birine ait ürün bulunamadı.")))
        };
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.Create(input, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.IsType<OrderDraftFormPageViewModel>(viewResult.Model);
        var key = $"{nameof(OrderDraftFormPageViewModel.Input)}.{nameof(OrderDraftInputModel.Items)}";
        var error = Assert.Single(controller.ModelState[key]!.Errors);
        Assert.Contains("ürün bulunamadı", error.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditGet_LoadsOnlyDraftAndMapsPersistedValuesToSafeInput()
    {
        var editModel = CreateDraftEdit();
        var queryService = new StubOrderQueryService
        {
            GetDraftForEditHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDraftEditViewModel>.Success(editModel))
        };
        var controller = CreateController(queryService);

        var actionResult = await controller.Edit(editModel.Id, "/Orders?Status=Draft", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<OrderDraftFormPageViewModel>(viewResult.Model);
        Assert.Equal(editModel.Id, page.OrderId);
        Assert.Equal(editModel.OrderNumber, page.OrderNumber);
        Assert.Equal(editModel.TotalAmount, page.CurrentTotalAmount);
        Assert.Equal(OrderType.Purchase, page.Input.Type);
        Assert.Equal(editModel.SupplierId, page.Input.SupplierId);
        var item = Assert.Single(page.Input.Items);
        Assert.Equal(9, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("/Orders?Status=Draft", page.ReturnUrl);
    }

    [Fact]
    public async Task EditPost_ForwardsDraftInputAndRedirectsToDetails()
    {
        var input = ValidSaleInput();
        var orderService = new StubOrderService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Success(new OrderMutationResult(
                    17,
                    "ORDER-17",
                    OrderStatus.Draft,
                    25m)))
        };
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.Edit(17, "/Orders", input, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["id"]);
        Assert.Equal(17, orderService.ReceivedOrderId);
        Assert.Same(input, orderService.ReceivedInput);
        Assert.Equal("Taslak sipariş başarıyla güncellendi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task EditPost_WhenOrderBecomesTerminal_ReturnsDetailsWithConflictAndSafeMessage()
    {
        var detail = CreateDetail();
        var queryService = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        var orderService = new StubOrderService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Failure(new ServiceError(
                    ServiceErrorCategory.BusinessRule,
                    OrderServiceErrorCodes.OrderNotDraft,
                    "Yalnızca taslak siparişler düzenlenebilir.")))
        };
        var controller = CreateController(queryService, orderService);

        var actionResult = await controller.Edit(
            detail.Id,
            "/Orders",
            ValidSaleInput(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), viewResult.ViewName);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Yalnızca taslak siparişler düzenlenebilir.");
    }

    [Theory]
    [InlineData(nameof(OrdersController.Confirm))]
    [InlineData(nameof(OrdersController.Cancel))]
    [InlineData(nameof(OrdersController.Delete))]
    public async Task DraftActionGet_WhenOrderIsDraft_ReturnsConfirmationView(string actionName)
    {
        var detail = CreateDetail(OrderStatus.Draft);
        var queryService = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(queryService);

        var actionResult = actionName switch
        {
            nameof(OrdersController.Confirm) => await controller.Confirm(
                detail.Id,
                cancellationTokenSource.Token),
            nameof(OrdersController.Cancel) => await controller.Cancel(
                detail.Id,
                cancellationTokenSource.Token),
            nameof(OrdersController.Delete) => await controller.Delete(
                detail.Id,
                cancellationTokenSource.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(actionName))
        };

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(actionName, viewResult.ViewName);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(detail.Id, queryService.ReceivedOrderId);
        Assert.Equal(cancellationTokenSource.Token, queryService.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData(nameof(OrdersController.Confirm), OrderStatus.Confirmed)]
    [InlineData(nameof(OrdersController.Cancel), OrderStatus.Cancelled)]
    [InlineData(nameof(OrdersController.Delete), OrderStatus.Confirmed)]
    public async Task DraftActionGet_WhenOrderIsTerminal_ReturnsDetailsConflict(
        string actionName,
        OrderStatus status)
    {
        var detail = CreateDetail(status);
        var queryService = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        var controller = CreateController(queryService);

        var actionResult = actionName switch
        {
            nameof(OrdersController.Confirm) => await controller.Confirm(
                detail.Id,
                CancellationToken.None),
            nameof(OrdersController.Cancel) => await controller.Cancel(
                detail.Id,
                CancellationToken.None),
            nameof(OrdersController.Delete) => await controller.Delete(
                detail.Id,
                CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(actionName))
        };

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), viewResult.ViewName);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("taslak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConfirmPost_ForwardsRequestAndRedirectsToDetails()
    {
        var orderService = new StubOrderService
        {
            ConfirmHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Success(new OrderMutationResult(
                    17,
                    "ORDER-17",
                    OrderStatus.Confirmed,
                    25m)))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.ConfirmConfirmed(17, cancellationTokenSource.Token);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["id"]);
        Assert.Equal(17, orderService.ReceivedOrderId);
        Assert.Equal(cancellationTokenSource.Token, orderService.ReceivedCancellationToken);
        Assert.Equal("Sipariş başarıyla onaylandı.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task CancelPost_ForwardsRequestAndRedirectsToDetails()
    {
        var orderService = new StubOrderService
        {
            CancelHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Success(new OrderMutationResult(
                    17,
                    "ORDER-17",
                    OrderStatus.Cancelled,
                    25m)))
        };
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.CancelConfirmed(17, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["id"]);
        Assert.Equal(17, orderService.ReceivedOrderId);
        Assert.Equal("Sipariş başarıyla iptal edildi.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task DeletePost_ForwardsRequestAndRedirectsToIndex()
    {
        var orderService = new StubOrderService
        {
            DeleteHandler = (_, _) => Task.FromResult(ServiceResult.Success())
        };
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.DeleteConfirmed(17, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Index), redirect.ActionName);
        Assert.Equal(17, orderService.ReceivedOrderId);
        Assert.Equal("Taslak sipariş başarıyla silindi.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(ServiceErrorCategory.Validation, OrderServiceErrorCodes.InvalidQuantity, StatusCodes.Status400BadRequest)]
    [InlineData(ServiceErrorCategory.BusinessRule, OrderServiceErrorCodes.InsufficientStock, StatusCodes.Status409Conflict)]
    public async Task ConfirmPost_WhenServiceReturnsExpectedFailure_ShowsConfirmationWithSafeStatus(
        ServiceErrorCategory category,
        string errorCode,
        int expectedStatusCode)
    {
        var detail = CreateDetail(OrderStatus.Draft);
        var queryService = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        var orderService = new StubOrderService
        {
            ConfirmHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Failure(new ServiceError(
                    category,
                    errorCode,
                    "Güvenli sipariş işlem mesajı.")))
        };
        var controller = CreateController(queryService, orderService);

        var actionResult = await controller.ConfirmConfirmed(detail.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Confirm), viewResult.ViewName);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(expectedStatusCode, controller.Response.StatusCode);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Güvenli sipariş işlem mesajı.");
    }

    [Fact]
    public async Task CancelPost_WhenServiceReturnsNotFound_ReturnsSafeNotFoundView()
    {
        var orderService = new StubOrderService
        {
            CancelHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderMutationResult>.Failure(new ServiceError(
                    ServiceErrorCategory.NotFound,
                    OrderServiceErrorCodes.OrderNotFound,
                    "Sipariş bulunamadı.")))
        };
        var controller = CreateController(new StubOrderQueryService(), orderService);

        var actionResult = await controller.CancelConfirmed(404, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task DeletePost_WhenOrderBecomesTerminal_ReturnsDetailsConflict()
    {
        var detail = CreateDetail(OrderStatus.Cancelled);
        var queryService = new StubOrderQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<OrderDetailViewModel>.Success(detail))
        };
        var orderService = new StubOrderService
        {
            DeleteHandler = (_, _) => Task.FromResult(
                ServiceResult.Failure(new ServiceError(
                    ServiceErrorCategory.BusinessRule,
                    OrderServiceErrorCodes.OrderNotDraft,
                    "Yalnızca taslak siparişler silinebilir.")))
        };
        var controller = CreateController(queryService, orderService);

        var actionResult = await controller.DeleteConfirmed(detail.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal(nameof(OrdersController.Details), viewResult.ViewName);
        Assert.Same(detail, viewResult.Model);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Yalnızca taslak siparişler silinebilir.");
    }

    [Fact]
    public void Controller_RequiresAdminOrEmployeeAndExposesProtectedDraftActionsWithoutDbContext()
    {
        var authorize = Assert.Single(
            typeof(OrdersController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal([AppRoles.Admin, AppRoles.Employee], SplitRoles(authorize.Roles));

        var actions = typeof(OrdersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(12, actions.Length);
        var getActions = actions.Where(action => action.GetCustomAttribute<HttpGetAttribute>() is not null).ToList();
        var postActions = actions.Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null).ToList();
        Assert.Equal(7, getActions.Count);
        Assert.Equal(5, postActions.Count);
        Assert.Equal(actions.Length, getActions.Count + postActions.Count);
        Assert.All(postActions, action =>
            Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));

        var adminActions = actions
            .Where(action => action.Name is
                nameof(OrdersController.Confirm)
                or nameof(OrdersController.ConfirmConfirmed)
                or nameof(OrdersController.Cancel)
                or nameof(OrdersController.CancelConfirmed)
                or nameof(OrdersController.Delete)
                or nameof(OrdersController.DeleteConfirmed))
            .ToList();
        Assert.Equal(6, adminActions.Count);
        Assert.All(adminActions, action =>
        {
            var actionAuthorize = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal([AppRoles.Admin], SplitRoles(actionAuthorize.Roles));
        });
        Assert.All(
            actions.Except(adminActions),
            action => Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>()));

        Assert.Equal(
            nameof(OrdersController.Confirm),
            typeof(OrdersController)
                .GetMethod(nameof(OrdersController.ConfirmConfirmed))!
                .GetCustomAttribute<ActionNameAttribute>()?.Name);
        Assert.Equal(
            nameof(OrdersController.Cancel),
            typeof(OrdersController)
                .GetMethod(nameof(OrdersController.CancelConfirmed))!
                .GetCustomAttribute<ActionNameAttribute>()?.Name);
        Assert.Equal(
            nameof(OrdersController.Delete),
            typeof(OrdersController)
                .GetMethod(nameof(OrdersController.DeleteConfirmed))!
                .GetCustomAttribute<ActionNameAttribute>()?.Name);

        var constructor = Assert.Single(typeof(OrdersController).GetConstructors());
        Assert.Equal(
            [
                typeof(IOrderQueryService),
                typeof(IOrderService),
                typeof(ICustomerService),
                typeof(ISupplierService),
                typeof(IProductService),
                typeof(ILogger<OrdersController>)
            ],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ApplicationDbContext));
    }

    [Fact]
    public async Task ControllerAuthorizationPolicy_AllowsAdminAndEmployeeAndRejectsOtherUsers()
    {
        var authorize = Assert.Single(
            typeof(OrdersController).GetCustomAttributes<AuthorizeAttribute>());
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
        var otherResult = await authorizationService.AuthorizeAsync(
            CreatePrincipal("Viewer"),
            resource: null,
            policy);

        Assert.True(adminResult.Succeeded);
        Assert.True(employeeResult.Succeeded);
        Assert.False(otherResult.Succeeded);
    }

    [Theory]
    [InlineData(nameof(OrdersController.Confirm))]
    [InlineData(nameof(OrdersController.ConfirmConfirmed))]
    [InlineData(nameof(OrdersController.Cancel))]
    [InlineData(nameof(OrdersController.CancelConfirmed))]
    [InlineData(nameof(OrdersController.Delete))]
    [InlineData(nameof(OrdersController.DeleteConfirmed))]
    public async Task DraftStateActionAuthorization_AllowsAdminAndRejectsEmployee(string methodName)
    {
        var method = typeof(OrdersController).GetMethod(methodName)!;
        var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
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

    private static readonly DateTime UtcDate =
        new(2026, 9, 2, 9, 30, 0, DateTimeKind.Utc);

    private static OrdersController CreateController(
        IOrderQueryService orderQueryService,
        IOrderService? orderService = null,
        ICustomerService? customerService = null,
        ISupplierService? supplierService = null,
        IProductService? productService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "order-trace",
            User = CreatePrincipal(AppRoles.Admin)
        };
        var controller = new OrdersController(
            orderQueryService,
            orderService ?? new StubOrderService(),
            customerService ?? new StubCustomerService(),
            supplierService ?? new StubSupplierService(),
            productService ?? new StubProductService(),
            NullLogger<OrdersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper("/Orders")
        };
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
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

    private static InvalidOperationException MissingHandlerException()
    {
        return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
    }

    private static OrderDetailViewModel CreateDetail(
        OrderStatus status = OrderStatus.Confirmed)
    {
        return new OrderDetailViewModel(
            17,
            "ORDER-17",
            OrderType.Sale,
            status,
            UtcDate,
            25m,
            7,
            "Test Customer",
            null,
            null,
            [new OrderItemViewModel(4, 9, "Test Product", "TEST-SKU", 2, 12.50m, 25m)]);
    }

    private static OrderDraftEditViewModel CreateDraftEdit()
    {
        return new OrderDraftEditViewModel(
            17,
            "ORDER-17",
            UtcDate,
            OrderType.Purchase,
            null,
            8,
            25m,
            [new OrderDraftEditItemViewModel(9, "Test Product", "TEST-SKU", 2, 12.50m)]);
    }

    private static OrderDraftInputModel ValidSaleInput()
    {
        return new OrderDraftInputModel
        {
            Type = OrderType.Sale,
            CustomerId = 7,
            Items = [new OrderItemInputModel { ProductId = 9, Quantity = 2 }]
        };
    }

    private sealed class StubOrderQueryService : IOrderQueryService
    {
        public Func<OrderListQueryModel?, CancellationToken, Task<ServiceResult<OrderListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<OrderDetailViewModel>>>?
            GetByIdHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<OrderDraftEditViewModel>>>?
            GetDraftForEditHandler
        { get; init; }

        public OrderListQueryModel? ReceivedListQuery { get; private set; }

        public int? ReceivedOrderId { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<OrderListViewModel>> GetListAsync(
            OrderListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedListQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<OrderDetailViewModel>> GetByIdAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(orderId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<OrderDraftEditViewModel>> GetDraftForEditAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedCancellationToken = cancellationToken;
            return GetDraftForEditHandler is not null
                ? GetDraftForEditHandler(orderId, cancellationToken)
                : throw MissingHandlerException();
        }

        private static InvalidOperationException MissingHandlerException()
        {
            return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
        }
    }

    private sealed class StubOrderService : IOrderService
    {
        public Func<OrderDraftInputModel, string, CancellationToken, Task<ServiceResult<OrderMutationResult>>>?
            CreateHandler
        { get; init; }

        public Func<int, OrderDraftInputModel, CancellationToken, Task<ServiceResult<OrderMutationResult>>>?
            UpdateHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<OrderMutationResult>>>?
            ConfirmHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<OrderMutationResult>>>?
            CancelHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult>>?
            DeleteHandler
        { get; init; }

        public OrderDraftInputModel? ReceivedInput { get; private set; }

        public string? ReceivedUserId { get; private set; }

        public int? ReceivedOrderId { get; private set; }

        public int CreateCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<OrderMutationResult>> CreateDraftAsync(
            OrderDraftInputModel input,
            string createdByUserId,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            ReceivedInput = input;
            ReceivedUserId = createdByUserId;
            ReceivedCancellationToken = cancellationToken;
            return CreateHandler is not null
                ? CreateHandler(input, createdByUserId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<OrderMutationResult>> UpdateDraftAsync(
            int orderId,
            OrderDraftInputModel input,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            ReceivedOrderId = orderId;
            ReceivedInput = input;
            ReceivedCancellationToken = cancellationToken;
            return UpdateHandler is not null
                ? UpdateHandler(orderId, input, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<OrderMutationResult>> ConfirmDraftAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedCancellationToken = cancellationToken;
            return ConfirmHandler is not null
                ? ConfirmHandler(orderId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<OrderMutationResult>> CancelDraftAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedCancellationToken = cancellationToken;
            return CancelHandler is not null
                ? CancelHandler(orderId, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult> DeleteDraftAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedCancellationToken = cancellationToken;
            return DeleteHandler is not null
                ? DeleteHandler(orderId, cancellationToken)
                : throw MissingHandlerException();
        }
    }

    private sealed class StubCustomerService : ICustomerService
    {
        public Task<ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CustomerSelectionOptionViewModel> options =
                [new CustomerSelectionOptionViewModel(7, "Test Customer")];
            return Task.FromResult(ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>.Success(options));
        }

        public Task<ServiceResult<CustomerListViewModel>> GetListAsync(CustomerListQueryModel? query = null, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<CustomerViewModel>> GetByIdAsync(int customerId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<CustomerViewModel>> CreateAsync(CustomerInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<CustomerViewModel>> UpdateAsync(int customerId, CustomerInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult> DeleteAsync(int customerId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
    }

    private sealed class StubSupplierService : ISupplierService
    {
        public Task<ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SupplierSelectionOptionViewModel> options =
                [new SupplierSelectionOptionViewModel(8, "Test Supplier")];
            return Task.FromResult(ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>.Success(options));
        }

        public Task<ServiceResult<SupplierListViewModel>> GetListAsync(SupplierListQueryModel? query = null, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<SupplierViewModel>> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<SupplierViewModel>> CreateAsync(SupplierInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<SupplierViewModel>> UpdateAsync(int supplierId, SupplierInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult> DeleteAsync(int supplierId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
    }

    private sealed class StubProductService : IProductService
    {
        public Task<ServiceResult<IReadOnlyList<ProductSelectionOptionViewModel>>> GetSelectionOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProductSelectionOptionViewModel> options =
                [new ProductSelectionOptionViewModel(9, "Test Product", "TEST-SKU")];
            return Task.FromResult(ServiceResult<IReadOnlyList<ProductSelectionOptionViewModel>>.Success(options));
        }

        public Task<ServiceResult<ProductListViewModel>> GetListAsync(ProductListQueryModel? query = null, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<ProductViewModel>> GetByIdAsync(int productId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<ProductViewModel>> CreateAsync(ProductCreateInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult<ProductViewModel>> UpdateAsync(int productId, ProductUpdateInputModel? input, CancellationToken cancellationToken = default) => throw MissingHandlerException();
        public Task<ServiceResult> DeleteAsync(int productId, CancellationToken cancellationToken = default) => throw MissingHandlerException();
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class TestUnexpectedException : Exception;
}
