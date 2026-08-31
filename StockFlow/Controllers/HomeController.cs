using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Models;
using StockFlow.Security;
using StockFlow.Services.Dashboard;

namespace StockFlow.Controllers;

public sealed class HomeController(
    IDashboardService dashboardService,
    ILogger<HomeController> logger) : Controller
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetAsync(cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogError(
                "Dashboard verileri alınamadı. ErrorCode: {ErrorCode}, TraceIdentifier: {TraceIdentifier}",
                result.Error?.Code ?? "Dashboard.UnexpectedResult",
                HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View("Error", CreateErrorViewModel());
        }

        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(CreateErrorViewModel());
    }

    private ErrorViewModel CreateErrorViewModel()
    {
        return new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };
    }
}
