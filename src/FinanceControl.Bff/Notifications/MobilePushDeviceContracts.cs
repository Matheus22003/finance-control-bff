namespace FinanceControl.Bff.Notifications;

public sealed record UpsertMobilePushDeviceRequest(
    string InstallationId,
    string Platform,
    string ExpoPushToken,
    string? DeviceName,
    string? AppVersion);

/// <summary>
/// Registration metadata is returned without the Expo token. The token is retained server-side
/// for the future provider sender and is never sent to a different authenticated user.
/// </summary>
public sealed record MobilePushDeviceResponse(
    Guid Id,
    string InstallationId,
    string Platform,
    string? DeviceName,
    string? AppVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
