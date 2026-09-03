namespace FinanceControl.Bff.Contracts.Auth;

public sealed record MobileRefreshRequest(
    string RefreshToken,
    string DeviceInstallationId);
