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
using StockFlow.Services.StockMovements;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Tests.Controllers;

public sealed class StockMovementsControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndTokenAndReturnsNormalizedPageModel()
    {
        var query = new StockMovementListQueryModel
        {
            ProductId = 9,
            OrderId = 17,
            Type = StockMovementType.StockIn,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 3),
            SortOrder = StockMovementSortOrder.DateAscending,
            Page = 2,
            PageSize = 10
        };
        var list = CreateList();
        var service = new StubStockMovementQueryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementListViewModel>.Success(list))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Index(query, cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<StockMovementListPageViewModel>(viewResult.Model);
        Assert.Same(list, page.Results);
        Assert.Equal(list.ProductId, page.ProductId);
        Assert.Equal(list.OrderId, page.OrderId);
        Assert.Equal(list.Type, page.Type);
        Assert.Equal(list.StartDate, page.StartDate);
        Assert.Equal(list.EndDate, page.EndDate);
        Assert.Equal(list.SortOrder, page.SortOrder);
        Assert.Equal(list.PageSize, page.PageSize);
        Assert.Same(query, service.ReceivedQuery);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Index_WhenModelBindingIsInvalid_ReturnsBadRequestWithoutCallingService()
    {
        var query = new StockMovementListQueryModel
        {
            ProductId = 9,
            StartDate = new DateOnly(2026, 9, 3)
        };
        var service = new StubStockMovementQueryService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(StockMovementListQueryModel.EndDate), "Geçersiz tarih.");

        var actionResult = await controller.Index(query, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<StockMovementListPageViewModel>(viewResult.Model);
        Assert.Null(page.Results);
        Assert.Equal(query.ProductId, page.ProductId);
        Assert.Equal(query.StartDate, page.StartDate);
        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Equal(0, service.GetListCallCount);
    }

    [Fact]
    public async Task Index_WhenServiceRejectsDateRange_ReturnsBadRequestWithSafeMessage()
    {
        var query = new StockMovementListQueryModel
        {
            StartDate = new DateOnly(2026, 9, 3),
            EndDate = new DateOnly(2026, 9, 1)
        };
        var service = new StubStockMovementQueryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementListViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.Validation,
                    StockMovementQueryServiceErrorCodes.InvalidDateRange,
                    "Başlangıç tarihi bitiş tarihinden sonra olamaz.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Index(query, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var page = Assert.IsType<StockMovementListPageViewModel>(viewResult.Model);
        Assert.Null(page.Results);
        Assert.Equal(query.StartDate, page.StartDate);
        Assert.Equal(query.EndDate, page.EndDate);
        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Başlangıç tarihi bitiş tarihinden sonra olamaz.");
    }

    [Fact]
    public async Task Index_WhenServiceReturnsUnexpectedFailure_ReturnsSafeErrorView()
    {
        var service = new StubStockMovementQueryService
        {
            GetListHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementListViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.BusinessRule,
                    "stock_movement.unexpected",
                    "Kullanıcıya taşınmaması gereken ayrıntı.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Index(
            new StockMovementListQueryModel(),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Index_WhenServiceThrows_DoesNotSwallowException()
    {
        var service = new StubStockMovementQueryService
        {
            GetListHandler = (_, _) => throw new TestUnexpectedException()
        };
        var controller = CreateController(service);

        await Assert.ThrowsAsync<TestUnexpectedException>(() => controller.Index(
            new StockMovementListQueryModel(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Details_ForwardsIdAndTokenAndReturnsSafeProjection()
    {
        var movement = CreateMovement();
        var service = new StubStockMovementQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementViewModel>.Success(movement))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Details(
            movement.Id,
            cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Same(movement, viewResult.Model);
        Assert.Equal(movement.Id, service.ReceivedStockMovementId);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Details_WhenMovementDoesNotExist_ReturnsSafeNotFoundView()
    {
        var service = new StubStockMovementQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementViewModel>.Failure(new ServiceError(
                    ServiceErrorCategory.NotFound,
                    StockMovementQueryServiceErrorCodes.StockMovementNotFound,
                    "Stok hareketi bulunamadı.")))
        };
        var controller = CreateController(service);

        var actionResult = await controller.Details(404, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("NotFound", viewResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(ServiceErrorCategory.Validation, StockMovementQueryServiceErrorCodes.InvalidDateRange)]
    [InlineData(ServiceErrorCategory.NotFound, "product.not_found")]
    [InlineData(ServiceErrorCategory.BusinessRule, "stock_movement.unexpected")]
    public async Task Details_WhenServiceReturnsUnexpectedFailure_ReturnsSafeErrorView(
        ServiceErrorCategory category,
        string errorCode)
    {
        var service = new StubStockMovementQueryService
        {
            GetByIdHandler = (_, _) => Task.FromResult(
                ServiceResult<StockMovementViewModel>.Failure(new ServiceError(
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
    public void Controller_ExposesOnlyRoleProtectedGetActionsWithoutDbContext()
    {
        var authorize = Assert.Single(
            typeof(StockMovementsController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal([AppRoles.Admin, AppRoles.Employee], SplitRoles(authorize.Roles));

        var actions = typeof(StockMovementsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(2, actions.Length);
        Assert.All(actions, action => Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>()));
        Assert.All(actions, action => Assert.Null(action.GetCustomAttribute<HttpPostAttribute>()));
        Assert.All(actions, action => Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>()));

        var constructor = Assert.Single(typeof(StockMovementsController).GetConstructors());
        Assert.Equal(
            [typeof(IStockMovementQueryService), typeof(ILogger<StockMovementsController>)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ApplicationDbContext));
    }

    [Fact]
    public async Task ControllerAuthorizationPolicy_AllowsAdminAndEmployeeAndRejectsOtherUsers()
    {
        var authorize = Assert.Single(
            typeof(StockMovementsController).GetCustomAttributes<AuthorizeAttribute>());
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

    private static StockMovementsController CreateController(
        IStockMovementQueryService service)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "stock-movement-trace",
            User = CreatePrincipal(AppRoles.Admin)
        };
        return new StockMovementsController(
            service,
            NullLogger<StockMovementsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"));
    }

    private static string[] SplitRoles(string? roles)
    {
        Assert.NotNull(roles);
        return roles.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static StockMovementListViewModel CreateList()
    {
        return new StockMovementListViewModel(
            [CreateMovement()],
            9,
            17,
            StockMovementType.StockIn,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            StockMovementSortOrder.DateAscending,
            2,
            10,
            11,
            2);
    }

    private static StockMovementViewModel CreateMovement()
    {
        return new StockMovementViewModel(
            23,
            9,
            "Test Product",
            "TEST-SKU",
            17,
            "ORDER-17",
            StockMovementType.StockIn,
            4,
            "Sipariş ORDER-17: satın alma stok girişi.",
            new DateTimeOffset(2026, 9, 2, 9, 30, 0, TimeSpan.Zero));
    }

    private sealed class StubStockMovementQueryService : IStockMovementQueryService
    {
        public Func<StockMovementListQueryModel?, CancellationToken, Task<ServiceResult<StockMovementListViewModel>>>?
            GetListHandler
        { get; init; }

        public Func<int, CancellationToken, Task<ServiceResult<StockMovementViewModel>>>?
            GetByIdHandler
        { get; init; }

        public StockMovementListQueryModel? ReceivedQuery { get; private set; }

        public int? ReceivedStockMovementId { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public int GetListCallCount { get; private set; }

        public Task<ServiceResult<StockMovementListViewModel>> GetListAsync(
            StockMovementListQueryModel? query = null,
            CancellationToken cancellationToken = default)
        {
            GetListCallCount++;
            ReceivedQuery = query;
            ReceivedCancellationToken = cancellationToken;
            return GetListHandler is not null
                ? GetListHandler(query, cancellationToken)
                : throw MissingHandlerException();
        }

        public Task<ServiceResult<StockMovementViewModel>> GetByIdAsync(
            int stockMovementId,
            CancellationToken cancellationToken = default)
        {
            ReceivedStockMovementId = stockMovementId;
            ReceivedCancellationToken = cancellationToken;
            return GetByIdHandler is not null
                ? GetByIdHandler(stockMovementId, cancellationToken)
                : throw MissingHandlerException();
        }
    }

    private static InvalidOperationException MissingHandlerException()
    {
        return new InvalidOperationException("Beklenmeyen Service çağrısı yapıldı.");
    }

    private sealed class TestUnexpectedException : Exception;
}
