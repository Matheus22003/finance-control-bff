namespace FinanceControl.Bff.Contracts.Auth;

public sealed record MobileLoginRequest(
    string Email,
    string Password,
    string DeviceInstallationId,
    string DeviceName,
    string Platform,
    string AppVersion);
