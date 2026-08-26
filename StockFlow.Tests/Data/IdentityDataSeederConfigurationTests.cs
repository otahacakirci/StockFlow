using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Options;
using StockFlow.Tests.Infrastructure;

namespace StockFlow.Tests.Data;

public sealed class IdentityDataSeederConfigurationTests
{
    [Fact]
    public async Task ResolveSeeder_WhenConfigurationIsMissing_FailsBeforeDatabaseAccess()
    {
        await using var serviceProvider = CreateServiceProvider();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            _ = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
            await Task.CompletedTask;
        });

        Assert.Contains(
            exception.Failures,
            failure => failure.StartsWith("IdentitySeed:Admin:Email", StringComparison.Ordinal));
        Assert.Contains(
            exception.Failures,
            failure => failure.StartsWith("IdentitySeed:Employee:Password", StringComparison.Ordinal));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        SqlServerTestDatabase.AddUninitializedDbContext(services);
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services
            .AddOptions<IdentitySeedOptions>()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdentitySeedOptions>, IdentitySeedOptionsValidator>();
        services.AddScoped<IdentityDataSeeder>();

        return services.BuildServiceProvider();
    }
}
