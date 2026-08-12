namespace FinanceControl.Bff.Contracts.Auth;

public sealed record SessionResponse(
    Guid Id,
    string DeviceName,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);
