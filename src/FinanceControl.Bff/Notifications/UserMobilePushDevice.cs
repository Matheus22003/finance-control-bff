namespace FinanceControl.Bff.Notifications;

/// <summary>
/// A mobile installation registered for push delivery through Expo.
/// </summary>
public sealed class UserMobilePushDevice
{
    private UserMobilePushDevice()
    {
    }

    public UserMobilePushDevice(
        Guid userId,
        string installationId,
        string platform,
        string expoPushToken,
        string? deviceName,
        string? appVersion,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        InstallationId = installationId.Trim();
        Platform = NormalizePlatform(platform);
        CreatedAt = createdAt;
        Update(expoPushToken, deviceName, appVersion, createdAt);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string InstallationId { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public string ExpoPushToken { get; private set; } = string.Empty;

    public string? DeviceName { get; private set; }

    public string? AppVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string expoPushToken,
        string? deviceName,
        string? appVersion,
        DateTimeOffset updatedAt)
    {
        ExpoPushToken = expoPushToken.Trim();
        DeviceName = NormalizeOptional(deviceName);
        AppVersion = NormalizeOptional(appVersion);
        UpdatedAt = updatedAt;
    }

    public static string NormalizePlatform(string platform) =>
        platform.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
