namespace FinanceControl.Bff.Contracts.Auth;

public sealed record MobileSessionResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string DeviceInstallationId,
    AuthUserResponse User);
