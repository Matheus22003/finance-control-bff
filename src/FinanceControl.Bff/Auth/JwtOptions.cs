using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    public string Key { get; init; } = string.Empty;

    [Range(1, 1_440)]
    public int ExpiresMinutes { get; init; } = 60;
}
