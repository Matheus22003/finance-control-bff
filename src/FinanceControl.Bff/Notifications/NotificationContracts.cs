namespace FinanceControl.Bff.Notifications;

public sealed record NotificationPreferenceItemResponse(
    string Type,
    string Category,
    string Label,
    bool InAppEnabled,
    bool PushEnabled,
    bool EmailEnabled);

public sealed record NotificationPreferencesResponse(
    IReadOnlyList<NotificationPreferenceItemResponse> Preferences);

public sealed record UpdateNotificationPreferenceItemRequest(
    string Type,
    bool InAppEnabled,
    bool PushEnabled,
    bool EmailEnabled);

public sealed record UpdateNotificationPreferencesRequest(
    IReadOnlyList<UpdateNotificationPreferenceItemRequest> Preferences);

public sealed record PushNotificationConfigurationResponse(
    bool IsConfigured,
    string? PublicKey);

public sealed record CreatePushSubscriptionRequest(
    string Endpoint,
    string P256Dh,
    string Auth,
    string DeviceName);

public sealed record RemovePushSubscriptionRequest(string Endpoint);

public sealed record PushSubscriptionResponse(
    Guid Id,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
