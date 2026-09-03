using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StockFlow.Tests.Integration;

internal sealed class StockFlowWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    internal const string AdminEmail = "admin.mvc@stockflow.test";
    internal const string EmployeeEmail = "employee.mvc@stockflow.test";
    internal const string ValidTestCredential = "Test-only1!Credential";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["IdentitySeed:Admin:Email"] = AdminEmail,
                ["IdentitySeed:Admin:Password"] = ValidTestCredential,
                ["IdentitySeed:Employee:Email"] = EmployeeEmail,
                ["IdentitySeed:Employee:Password"] = ValidTestCredential
            });
        });

    }
}

public abstract class MvcIntegrationTestBase : IAsyncLifetime
{
    private readonly StockFlow.Tests.Infrastructure.SqlServerTestDatabase _testDatabase =
        StockFlow.Tests.Infrastructure.SqlServerTestDatabase.Create();
    private StockFlowWebApplicationFactory? _factory;

    internal StockFlowWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("MVC test hostu başlatılmadı.");

    protected HttpClient CreateClient(bool allowAutoRedirect = false)
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
    }

    public async Task InitializeAsync()
    {
        await _testDatabase.InitializeAsync();
        _factory = new StockFlowWebApplicationFactory(_testDatabase.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _testDatabase.DisposeAsync();
    }
}

internal static partial class MvcTestClient
{
    [GeneratedRegex(
        "<input(?=[^>]*name=\"__RequestVerificationToken\")(?=[^>]*value=\"(?<value>[^\"]+)\")[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryInputRegex();

    internal static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return ExtractAntiforgeryToken(html);
    }

    internal static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string? returnUrl = null)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync(
            "/Account/Login",
            CreateForm(
                ("Email", email),
                ("Password", StockFlowWebApplicationFactory.ValidTestCredential),
                ("RememberMe", "false"),
                ("ReturnUrl", returnUrl ?? string.Empty),
                ("__RequestVerificationToken", token)));
    }

    internal static FormUrlEncodedContent CreateForm(
        params (string Key, string Value)[] values)
    {
        return new FormUrlEncodedContent(values.Select(value =>
            new KeyValuePair<string, string>(value.Key, value.Value)));
    }

    internal static string ExtractAntiforgeryToken(string html)
    {
        var match = AntiforgeryInputRegex().Match(html);
        Assert.True(match.Success, "Yanıtta antiforgery alanı bulunamadı.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }
}
