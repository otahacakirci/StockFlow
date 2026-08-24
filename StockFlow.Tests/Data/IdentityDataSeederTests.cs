using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Options;
using StockFlow.Security;

namespace StockFlow.Tests.Data;

public sealed class IdentityDataSeederTests
{
    private const string AdminEmail = "admin@stockflow.test";
    private const string EmployeeEmail = "employee@stockflow.test";
    private const string ExistingUserCredential = "Existing1!Credential";
    private const string ValidTestCredential = "Test-only1!Credential";

    [Fact]
    public async Task SeedAsync_WhenRunTwice_CreatesOneUserAndMembershipPerRole()
    {
        await using var serviceProvider = CreateServiceProvider();

        await EnsureDatabaseCreatedAsync(serviceProvider);
        await RunSeederAsync(serviceProvider);
        await RunSeederAsync(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(2, await dbContext.Roles.CountAsync());
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(2, await dbContext.UserRoles.CountAsync());

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        var employee = await userManager.FindByEmailAsync(EmployeeEmail);

        Assert.NotNull(admin);
        Assert.NotNull(employee);
        Assert.True(await userManager.IsInRoleAsync(admin, AppRoles.Admin));
        Assert.True(await userManager.IsInRoleAsync(employee, AppRoles.Employee));
        Assert.True(await userManager.CheckPasswordAsync(admin, ValidTestCredential));
        Assert.True(await userManager.CheckPasswordAsync(employee, ValidTestCredential));
    }

    [Fact]
    public async Task SeedAsync_WhenConfiguredUserExists_AddsMissingRoleWithoutDuplicateUser()
    {
        await using var serviceProvider = CreateServiceProvider();

        await EnsureDatabaseCreatedAsync(serviceProvider);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = new ApplicationUser
            {
                UserName = "existing-admin",
                Email = AdminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(existingAdmin, ExistingUserCredential);
            Assert.True(createResult.Succeeded);
        }

        await RunSeederAsync(serviceProvider);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var verificationUserManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await verificationUserManager.FindByEmailAsync(AdminEmail);

        Assert.NotNull(admin);
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.True(await verificationUserManager.IsInRoleAsync(admin, AppRoles.Admin));
        Assert.True(await verificationUserManager.CheckPasswordAsync(admin, ExistingUserCredential));
        Assert.False(await verificationUserManager.CheckPasswordAsync(admin, ValidTestCredential));
    }

    [Fact]
    public async Task ResolveSeeder_WhenConfigurationIsMissing_FailsBeforeDatabaseAccess()
    {
        await using var serviceProvider = CreateServiceProvider(configureSeedUsers: false);

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

    private static ServiceProvider CreateServiceProvider(bool configureSeedUsers = true)
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var optionsBuilder = services
            .AddOptions<IdentitySeedOptions>()
            .ValidateOnStart();

        if (configureSeedUsers)
        {
            optionsBuilder.Configure(options =>
            {
                options.Admin.Email = AdminEmail;
                options.Admin.Password = ValidTestCredential;
                options.Employee.Email = EmployeeEmail;
                options.Employee.Password = ValidTestCredential;
            });
        }

        services.AddSingleton<IValidateOptions<IdentitySeedOptions>, IdentitySeedOptionsValidator>();
        services.AddScoped<IdentityDataSeeder>();

        return services.BuildServiceProvider();
    }

    private static async Task EnsureDatabaseCreatedAsync(ServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private static async Task RunSeederAsync(ServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        await seeder.SeedAsync();
    }
}
