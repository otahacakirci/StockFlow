using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Controllers;
using StockFlow.Entities;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Dashboard;
using StockFlow.ViewModels.Dashboard;

namespace StockFlow.Tests.Controllers;

public sealed class HomeControllerTests
{
    [Fact]
    public async Task Index_WhenDashboardSucceeds_ReturnsViewModelAndForwardsCancellationToken()
    {
        var dashboard = CreateDashboard();
        var service = new StubDashboardService(
            ServiceResult<DashboardViewModel>.Success(dashboard));
        using var cancellationTokenSource = new CancellationTokenSource();
        var controller = CreateController(service);

        var actionResult = await controller.Index(cancellationTokenSource.Token);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Null(viewResult.ViewName);
        Assert.Same(dashboard, viewResult.Model);
        Assert.Equal(cancellationTokenSource.Token, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Index_WhenDashboardFails_ReturnsSafeErrorViewWithServerErrorStatus()
    {
        var serviceError = new ServiceError(
            ServiceErrorCategory.BusinessRule,
            "Dashboard.Unavailable",
            "Kullanıcıya taşınmaması gereken teknik ayrıntı.");
        var service = new StubDashboardService(
            ServiceResult<DashboardViewModel>.Failure(serviceError));
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "dashboard-trace"
        };
        var controller = CreateController(service, httpContext);

        var actionResult = await controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        Assert.Equal("Error", viewResult.ViewName);
        var errorModel = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal(httpContext.TraceIdentifier, errorModel.RequestId);
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            httpContext.Response.StatusCode);
    }

    [Fact]
    public void Index_RequiresExactlyAdminOrEmployeeRole()
    {
        var indexMethod = typeof(HomeController).GetMethod(nameof(HomeController.Index));

        Assert.NotNull(indexMethod);
        var authorizeAttribute = Assert.Single(
            indexMethod.GetCustomAttributes<AuthorizeAttribute>());
        Assert.NotNull(authorizeAttribute.Roles);
        var roles = authorizeAttribute.Roles.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal([AppRoles.Admin, AppRoles.Employee], roles);
    }

    private static HomeController CreateController(
        IDashboardService dashboardService,
        HttpContext? httpContext = null)
    {
        return new HomeController(
            dashboardService,
            NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext ?? new DefaultHttpContext()
            }
        };
    }

    private static DashboardViewModel CreateDashboard()
    {
        return new DashboardViewModel(
            12,
            2,
            5,
            3,
            8,
            1250m,
            [
                new DashboardRecentOrderViewModel(
                    17,
                    "ORD-17",
                    OrderType.Sale,
                    OrderStatus.Confirmed,
                    new DateTime(2026, 8, 31, 9, 30, 0, DateTimeKind.Utc),
                    1250m,
                    "Test Customer")
            ]);
    }

    private sealed class StubDashboardService(
        ServiceResult<DashboardViewModel> result) : IDashboardService
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ServiceResult<DashboardViewModel>> GetAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
