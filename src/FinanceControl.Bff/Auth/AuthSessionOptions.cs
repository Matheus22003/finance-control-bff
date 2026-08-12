using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Auth;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSession";

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 30;

    [Required]
    public string CookieName { get; init; } = "finance_control_refresh";
}
