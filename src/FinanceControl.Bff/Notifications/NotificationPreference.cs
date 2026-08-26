namespace FinanceControl.Bff.Notifications;

public sealed class NotificationPreference
{
    private NotificationPreference()
    {
    }

    public NotificationPreference(
        Guid userId,
        NotificationType type,
        bool inAppEnabled,
        bool pushEnabled,
        bool emailEnabled,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        Type = type;
        Update(inAppEnabled, pushEnabled, emailEnabled, updatedAt);
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public bool InAppEnabled { get; private set; }

    public bool PushEnabled { get; private set; }

    public bool EmailEnabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        bool inAppEnabled,
        bool pushEnabled,
        bool emailEnabled,
        DateTimeOffset updatedAt)
    {
        InAppEnabled = inAppEnabled;
        PushEnabled = pushEnabled;
        EmailEnabled = emailEnabled;
        UpdatedAt = updatedAt;
    }
}
