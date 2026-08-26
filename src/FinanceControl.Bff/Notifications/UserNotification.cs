namespace FinanceControl.Bff.Notifications;

public sealed class UserNotification
{
    private UserNotification()
    {
    }

    public UserNotification(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? route,
        DateTimeOffset createdAt,
        string? deduplicationKey = null,
        bool isVisibleInApp = true)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Title = title.Trim();
        Message = message.Trim();
        Route = string.IsNullOrWhiteSpace(route) ? null : route.Trim();
        DeduplicationKey = string.IsNullOrWhiteSpace(deduplicationKey)
            ? null
            : deduplicationKey.Trim();
        CreatedAt = createdAt;
        IsVisibleInApp = isVisibleInApp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? Route { get; private set; }

    public string? DeduplicationKey { get; private set; }

    public bool IsRead { get; private set; }

    public bool IsVisibleInApp { get; private set; } = true;

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkAsRead(DateTimeOffset readAt)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = readAt;
    }
}
