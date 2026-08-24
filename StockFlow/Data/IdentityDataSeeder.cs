using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StockFlow.Entities;
using StockFlow.Options;
using StockFlow.Security;

namespace StockFlow.Data;

public sealed class IdentityDataSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentitySeedOptions> options,
    ILogger<IdentityDataSeeder> logger)
{
    private readonly IdentitySeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(AppRoles.Admin, cancellationToken);
        await EnsureRoleAsync(AppRoles.Employee, cancellationToken);

        await EnsureUserAsync(_options.Admin, AppRoles.Admin, cancellationToken);
        await EnsureUserAsync(_options.Employee, AppRoles.Employee, cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        EnsureSucceeded(result, $"{roleName} rolü oluşturulamadı");
        logger.LogInformation("Identity rolü oluşturuldu: {RoleName}.", roleName);
    }

    private async Task EnsureUserAsync(
        IdentitySeedUserOptions seedUser,
        string roleName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(seedUser.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = seedUser.Email,
                Email = seedUser.Email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, seedUser.Password);
            EnsureSucceeded(createResult, $"{roleName} başlangıç kullanıcısı oluşturulamadı");
            logger.LogInformation("{RoleName} rolü için başlangıç kullanıcısı oluşturuldu.", roleName);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, roleName);
        EnsureSucceeded(addToRoleResult, $"Başlangıç kullanıcısı {roleName} rolüne eklenemedi");
        logger.LogInformation("Başlangıç kullanıcısına {RoleName} rolü atandı.", roleName);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(
            ", ",
            result.Errors
                .Select(error => error.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal));

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(errorCodes)
                ? $"{operation}."
                : $"{operation}. Identity hata kodları: {errorCodes}.");
    }
}
