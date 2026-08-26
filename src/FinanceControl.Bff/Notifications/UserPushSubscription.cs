using System.Security.Cryptography;
using System.Text;

namespace FinanceControl.Bff.Notifications;

public sealed class UserPushSubscription
{
    private UserPushSubscription()
    {
    }

    public UserPushSubscription(
        Guid userId,
        string endpoint,
        string p256Dh,
        string auth,
        string deviceName,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAt = createdAt;
        Update(endpoint, p256Dh, auth, deviceName, createdAt);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string EndpointHash { get; private set; } = string.Empty;

    public string P256Dh { get; private set; } = string.Empty;

    public string Auth { get; private set; } = string.Empty;

    public string DeviceName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string endpoint,
        string p256Dh,
        string auth,
        string deviceName,
        DateTimeOffset updatedAt)
    {
        Endpoint = endpoint.Trim();
        EndpointHash = HashEndpoint(Endpoint);
        P256Dh = p256Dh.Trim();
        Auth = auth.Trim();
        DeviceName = deviceName.Trim();
        UpdatedAt = updatedAt;
    }

    public static string HashEndpoint(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.Trim())));
}
