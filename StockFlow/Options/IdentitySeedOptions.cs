using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace StockFlow.Options;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public IdentitySeedUserOptions Admin { get; set; } = new();

    public IdentitySeedUserOptions Employee { get; set; } = new();
}

public sealed class IdentitySeedUserOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class IdentitySeedOptionsValidator : IValidateOptions<IdentitySeedOptions>
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public ValidateOptionsResult Validate(string? name, IdentitySeedOptions options)
    {
        var failures = new List<string>();

        ValidateUser(options.Admin, "Admin", failures);
        ValidateUser(options.Employee, "Employee", failures);

        if (!string.IsNullOrWhiteSpace(options.Admin.Email) &&
            !string.IsNullOrWhiteSpace(options.Employee.Email) &&
            string.Equals(options.Admin.Email, options.Employee.Email, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Admin ve Employee başlangıç kullanıcıları farklı e-posta adresleri kullanmalıdır.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateUser(
        IdentitySeedUserOptions user,
        string roleName,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            failures.Add($"IdentitySeed:{roleName}:Email yapılandırması zorunludur.");
        }
        else if (!EmailAddressValidator.IsValid(user.Email))
        {
            failures.Add($"IdentitySeed:{roleName}:Email geçerli bir e-posta adresi olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(user.Password))
        {
            failures.Add($"IdentitySeed:{roleName}:Password yapılandırması zorunludur.");
        }
    }
}
