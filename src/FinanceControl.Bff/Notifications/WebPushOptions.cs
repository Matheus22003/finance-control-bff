namespace FinanceControl.Bff.Notifications;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public bool Enabled { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string PublicKey { get; init; } = string.Empty;

    public string PrivateKey { get; init; } = string.Empty;

    public bool IsConfigured =>
        Enabled &&
        HasValidSubject() &&
        HasValidKey(PublicKey, 65, mustBeUncompressedPublicKey: true) &&
        HasValidKey(PrivateKey, 32, mustBeUncompressedPublicKey: false);

    private bool HasValidSubject() =>
        Uri.TryCreate(Subject, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "mailto");

    private static bool HasValidKey(
        string value,
        int expectedLength,
        bool mustBeUncompressedPublicKey)
    {
        try
        {
            var normalized = value
                .Replace('-', '+')
                .Replace('_', '/')
                .PadRight((value.Length + 3) / 4 * 4, '=');
            var bytes = Convert.FromBase64String(normalized);
            return bytes.Length == expectedLength &&
                   (!mustBeUncompressedPublicKey || bytes[0] == 4);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
