namespace FinanceControl.Bff.Notifications;

public sealed class MobilePushOptions
{
    public const string SectionName = "MobilePush";

    public bool Enabled { get; init; }

    public string ExpoApiUrl { get; init; } = "https://exp.host/--/api/v2/push/send";

    public int TimeoutSeconds { get; init; } = 10;

    public bool IsConfigured =>
        Enabled &&
        Uri.TryCreate(ExpoApiUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        TimeoutSeconds is > 0 and <= 60;
}
