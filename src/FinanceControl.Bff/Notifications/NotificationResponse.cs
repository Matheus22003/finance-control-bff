namespace FinanceControl.Bff.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? Route,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record NotificationUnreadCountResponse(int UnreadCount);

public sealed record NotificationSyncResponse(int CreatedCount, DateTimeOffset SyncedAt);
