using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Auth;

public sealed class DemoUserOptions
{
    public const string SectionName = "DemoUser";

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}
