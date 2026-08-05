namespace FinanceControl.Bff.Auth;

public sealed class UserSession
{
    public Guid Id { get; init; }
    public Guid FamilyId { get; init; }
    public Guid UserId { get; init; }
    public ApplicationUser User { get; init; } = null!;
    public string RefreshTokenHash { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string DeviceName { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
}
