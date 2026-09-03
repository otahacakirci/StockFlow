using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.ViewModels.Orders;

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
    public void Controller_RequiresAdminOrEmployeeAndExposesOnlyReadActions()
    {
        var authorize = Assert.Single(
            typeof(OrdersController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal([AppRoles.Admin, AppRoles.Employee], SplitRoles(authorize.Roles));

        var actions = typeof(OrdersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(2, actions.Length);
        Assert.All(actions, action =>
        {
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());
        });

        var constructor = Assert.Single(typeof(OrdersController).GetConstructors());
        Assert.Equal(
            [typeof(IOrderQueryService), typeof(ILogger<OrdersController>)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IOrderService)
                || parameter.ParameterType == typeof(ApplicationDbContext));
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

    private static readonly DateTime UtcDate =
        new(2026, 9, 2, 9, 30, 0, DateTimeKind.Utc);

    private static OrdersController CreateController(IOrderQueryService orderQueryService)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "order-trace"
        };
        return new OrdersController(
            orderQueryService,
            NullLogger<OrdersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
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

    private static OrderDetailViewModel CreateDetail()
    {
        return new OrderDetailViewModel(
            17,
            "ORDER-17",
            OrderType.Sale,
            OrderStatus.Confirmed,
            UtcDate,
            25m,
            7,
            "Test Customer",
            null,
            null,
            [new OrderItemViewModel(4, 9, "Test Product", "TEST-SKU", 2, 12.50m, 25m)]);
    }

    private sealed class StubOrderQueryService : IOrderQueryService
    {
        public Func<OrderListQueryModel?, CancellationToken, Task<ServiceResult<OrderListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<OrderDetailViewModel>>>?
            GetByIdHandler
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
            throw MissingHandlerException();
        }

        private static InvalidOperationException MissingHandlerException()
        {
            return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
        }
    }

    private sealed class TestUnexpectedException : Exception;
}
