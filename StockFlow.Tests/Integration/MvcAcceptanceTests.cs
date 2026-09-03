using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Dashboard;
using StockFlow.ViewModels.Dashboard;

namespace StockFlow.Tests.Integration;

public sealed class MvcAcceptanceTests : MvcIntegrationTestBase
{
    private const string InvalidTestCredential = "Invalid1!Credential";
    private const string TechnicalFailureMarker = "mvc-test-technical-detail";

    [Fact]
    public async Task AnonymousAccess_RedirectsProtectedEndpointAndAllowsPublicErrorPages()
    {
        using var client = CreateClient();

        using var protectedResponse = await client.GetAsync("/Products");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Contains(
            "/Account/Login",
            protectedResponse.Headers.Location?.OriginalString,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReturnUrl=%2FProducts",
            protectedResponse.Headers.Location?.OriginalString,
            StringComparison.Ordinal);

        using var loginResponse = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            "StockFlow'a giriş yap",
            await ReadHtmlAsync(loginResponse),
            StringComparison.Ordinal);

        using var accessDeniedResponse = await client.GetAsync("/Account/AccessDenied");
        Assert.Equal(HttpStatusCode.Forbidden, accessDeniedResponse.StatusCode);
        Assert.Contains(
            "Erişim reddedildi",
            await ReadHtmlAsync(accessDeniedResponse),
            StringComparison.Ordinal);

        using var errorResponse = await client.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.OK, errorResponse.StatusCode);
        Assert.Contains(
            "Beklenmeyen bir sorun oluştu",
            await ReadHtmlAsync(errorResponse),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_UsesGenericFailureAndRejectsExternalReturnUrl()
    {
        using var invalidClient = CreateClient();
        var token = await MvcTestClient.GetAntiforgeryTokenAsync(
            invalidClient,
            "/Account/Login?returnUrl=https%3A%2F%2Fexample.test%2Foutside");

        using var invalidResponse = await invalidClient.PostAsync(
            "/Account/Login",
            MvcTestClient.CreateForm(
                ("Email", StockFlowWebApplicationFactory.AdminEmail),
                ("Password", InvalidTestCredential),
                ("RememberMe", "false"),
                ("ReturnUrl", "https://example.test/outside"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidHtml = await ReadHtmlAsync(invalidResponse);
        Assert.Contains("E-posta veya parola hatalı.", invalidHtml, StringComparison.Ordinal);

        using var validClient = CreateClient();
        using var validResponse = await MvcTestClient.LoginAsync(
            validClient,
            StockFlowWebApplicationFactory.AdminEmail,
            "https://example.test/outside");

        Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);
        Assert.DoesNotContain(
            "example.test",
            validResponse.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employee_CanUseAssignedScreensAndDirectAdminRequestsAreDenied()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.EmployeeEmail);

        foreach (var path in new[]
                 {
                     "/",
                     "/Categories",
                     "/Products",
                     "/Customers/Create",
                     "/Orders/Create",
                     "/StockMovements"
                 })
        {
            using var allowedResponse = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        foreach (var path in new[]
                 {
                     "/Suppliers",
                     "/Categories/Create",
                     "/Products/Create",
                     "/Customers/Delete/1",
                     "/Orders/Confirm/1",
                     "/Orders/Cancel/1",
                     "/Orders/Delete/1"
                 })
        {
            using var deniedResponse = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, deniedResponse.StatusCode);
            Assert.Contains(
                "/Account/AccessDenied",
                deniedResponse.Headers.Location?.OriginalString,
                StringComparison.Ordinal);
        }

        using var dashboardResponse = await client.GetAsync("/");
        var dashboardHtml = await ReadHtmlAsync(dashboardResponse);
        Assert.DoesNotContain("href=\"/Suppliers\"", dashboardHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/StockMovements\"", dashboardHtml, StringComparison.Ordinal);

        using var productsResponse = await client.GetAsync("/Products");
        Assert.DoesNotContain(
            "Yeni ürün",
            await ReadHtmlAsync(productsResponse),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_SeesAndCanReachManagementScreens()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.AdminEmail);

        foreach (var path in new[] { "/Suppliers", "/Categories/Create", "/Products/Create" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var dashboardResponse = await client.GetAsync("/");
        Assert.Contains(
            "href=\"/Suppliers\"",
            await ReadHtmlAsync(dashboardResponse),
            StringComparison.Ordinal);

        using var productsResponse = await client.GetAsync("/Products");
        Assert.Contains(
            "Yeni ürün",
            await ReadHtmlAsync(productsResponse),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminPost_RequiresAntiforgeryAndSuccessfulMutationShowsNotification()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.AdminEmail);

        using var missingTokenResponse = await client.PostAsync(
            "/Categories/Create",
            MvcTestClient.CreateForm(("Name", "Korumasız kategori")));
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        var token = await MvcTestClient.GetAntiforgeryTokenAsync(client, "/Categories/Create");
        using var createResponse = await client.PostAsync(
            "/Categories/Create",
            MvcTestClient.CreateForm(
                ("Name", "MVC kabul kategorisi"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        using var detailResponse = await client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Contains(
            "Kategori başarıyla oluşturuldu.",
            await ReadHtmlAsync(detailResponse),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmployeeCustomerValidation_PreservesInputAndFieldMessage()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.EmployeeEmail);
        var token = await MvcTestClient.GetAntiforgeryTokenAsync(client, "/Customers/Create");

        using var response = await client.PostAsync(
            "/Customers/Create",
            MvcTestClient.CreateForm(
                ("Name", "Form değeri korunacak müşteri"),
                ("Email", "gecersiz-eposta"),
                ("Phone", string.Empty),
                ("Address", string.Empty),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("value=\"gecersiz-eposta\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "Geçerli bir e-posta adresi girilmelidir.",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_WithAntiforgeryEndsCookieSession()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.AdminEmail);
        var token = await MvcTestClient.GetAntiforgeryTokenAsync(client, "/");

        using var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            MvcTestClient.CreateForm(("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Contains(
            "/Account/Login",
            logoutResponse.Headers.Location?.OriginalString,
            StringComparison.Ordinal);

        using var protectedResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Contains(
            "/Account/Login",
            protectedResponse.Headers.Location?.OriginalString,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingProductAndStockMovement_ReturnDomainSpecificSafePages()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.EmployeeEmail);

        using var productResponse = await client.GetAsync("/Products/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, productResponse.StatusCode);
        var productHtml = await ReadHtmlAsync(productResponse);
        Assert.Contains("Aradığınız ürün mevcut değil", productHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/Products\"", productHtml, StringComparison.Ordinal);

        using var movementResponse = await client.GetAsync("/StockMovements/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, movementResponse.StatusCode);
        var movementHtml = await ReadHtmlAsync(movementResponse);
        Assert.Contains(
            "Aradığınız stok hareketi mevcut değil",
            movementHtml,
            StringComparison.Ordinal);
        Assert.Contains("href=\"/StockMovements\"", movementHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionException_ReturnsSafeErrorWithoutTechnicalDetails()
    {
        using var throwingFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDashboardService>();
                services.AddScoped<IDashboardService, ThrowingDashboardService>();
            });
        });
        using var client = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.AdminEmail);

        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("Beklenmeyen bir sorun oluştu", html, StringComparison.Ordinal);
        Assert.DoesNotContain(TechnicalFailureMarker, html, StringComparison.Ordinal);
        Assert.DoesNotContain("System.InvalidOperationException", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComplexListPagingLinks_PreserveFiltersAndResponsiveMarkup()
    {
        using var client = CreateClient();
        await LoginSuccessfullyAsync(client, StockFlowWebApplicationFactory.AdminEmail);
        var seededData = await SeedComplexListDataAsync();

        using var productsResponse = await client.GetAsync(
            $"/Products?SearchTerm=Kalem&CategoryId={seededData.CategoryId}" +
            "&LowStockOnly=true&SortOrder=PriceDescending&Page=1&PageSize=10");
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        var productsHtml = await ReadHtmlAsync(productsResponse);
        AssertPagingLink(
            productsHtml,
            "SearchTerm=Kalem",
            $"CategoryId={seededData.CategoryId}",
            "LowStockOnly=True",
            "SortOrder=PriceDescending");

        using var ordersResponse = await client.GetAsync(
            "/Orders?Type=Sale&Status=Draft&SortOrder=DateAscending&Page=1&PageSize=10");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        var ordersHtml = await ReadHtmlAsync(ordersResponse);
        AssertPagingLink(
            ordersHtml,
            "Type=Sale",
            "Status=Draft",
            "SortOrder=DateAscending");

        using var movementsResponse = await client.GetAsync(
            $"/StockMovements?ProductId={seededData.MovementProductId}" +
            $"&OrderId={seededData.MovementOrderId}&Type=StockOut" +
            "&StartDate=2026-09-01&EndDate=2026-09-30" +
            "&SortOrder=DateDescending&Page=1&PageSize=10");
        Assert.Equal(HttpStatusCode.OK, movementsResponse.StatusCode);
        var movementsHtml = await ReadHtmlAsync(movementsResponse);
        AssertPagingLink(
            movementsHtml,
            $"ProductId={seededData.MovementProductId}",
            $"OrderId={seededData.MovementOrderId}",
            "Type=StockOut",
            "StartDate=2026-09-01",
            "EndDate=2026-09-30",
            "SortOrder=DateDescending");
    }

    private static async Task LoginSuccessfullyAsync(HttpClient client, string email)
    {
        using var response = await MvcTestClient.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> ReadHtmlAsync(HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static void AssertPagingLink(string html, params string[] expectedValues)
    {
        Assert.Contains("Page=2", html, StringComparison.Ordinal);
        Assert.Contains("PageSize=10", html, StringComparison.Ordinal);
        Assert.Contains("table-responsive", html, StringComparison.Ordinal);
        Assert.Contains("navbar-expand-md", html, StringComparison.Ordinal);

        foreach (var expectedValue in expectedValues)
        {
            Assert.Contains(expectedValue, html, StringComparison.Ordinal);
        }
    }

    private async Task<SeededListData> SeedComplexListDataAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new Category { Name = "Sayfalama kategorisi" };
        var customer = new Customer { Name = "Sayfalama müşterisi" };
        var products = Enumerable.Range(1, 12)
            .Select(index => new Product
            {
                Name = $"Kalem {index:D2}",
                Sku = $"MVC-SKU-{index:D2}",
                Price = 10m + index,
                StockQuantity = 5,
                MinimumStockQuantity = 5,
                Category = category
            })
            .ToList();

        dbContext.Add(category);
        dbContext.Add(customer);
        dbContext.AddRange(products);
        await dbContext.SaveChangesAsync();

        var draftOrders = Enumerable.Range(1, 12)
            .Select(index =>
            {
                var order = new Order
                {
                    OrderNumber = $"MVC-DRAFT-{index:D2}",
                    Type = OrderType.Sale,
                    Status = OrderStatus.Draft,
                    OrderDate = new DateTime(2026, 9, index, 8, 0, 0, DateTimeKind.Utc),
                    TotalAmount = products[index - 1].Price,
                    CustomerId = customer.Id
                };
                order.Items.Add(new OrderItem
                {
                    ProductId = products[index - 1].Id,
                    Quantity = 1,
                    UnitPrice = products[index - 1].Price
                });
                return order;
            })
            .ToList();
        dbContext.Orders.AddRange(draftOrders);

        var movementOrder = new Order
        {
            OrderNumber = "MVC-CONFIRMED-01",
            Type = OrderType.Sale,
            Status = OrderStatus.Confirmed,
            OrderDate = new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc),
            TotalAmount = products[0].Price,
            CustomerId = customer.Id
        };
        movementOrder.Items.Add(new OrderItem
        {
            ProductId = products[0].Id,
            Quantity = 1,
            UnitPrice = products[0].Price
        });
        dbContext.Orders.Add(movementOrder);
        await dbContext.SaveChangesAsync();

        var movements = Enumerable.Range(1, 12)
            .Select(index => new StockMovement
            {
                OrderId = movementOrder.Id,
                ProductId = products[0].Id,
                Type = StockMovementType.StockOut,
                Quantity = 1,
                Description = $"MVC sayfalama hareketi {index:D2}",
                MovementDate = new DateTime(2026, 9, index, 9, 0, 0, DateTimeKind.Utc)
            });
        dbContext.StockMovements.AddRange(movements);
        await dbContext.SaveChangesAsync();

        return new SeededListData(category.Id, products[0].Id, movementOrder.Id);
    }

    private sealed record SeededListData(
        int CategoryId,
        int MovementProductId,
        int MovementOrderId);

    private sealed class ThrowingDashboardService : IDashboardService
    {
        public Task<ServiceResult<DashboardViewModel>> GetAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ServiceResult<DashboardViewModel>>(
                new InvalidOperationException(TechnicalFailureMarker));
        }
    }
}
