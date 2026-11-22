namespace FinanceControl.Bff.Notifications;

public sealed record MobilePushMessage(
    string Title,
    string Body,
    string? Route,
    string? NotificationId,
    string? Type);

public sealed record MobilePushTestRequest(
    string Title,
    string Body,
    string? Route);

public sealed record MobilePushSendResponse(
    bool Configured,
    int DeviceCount,
    int AcceptedCount,
    int RejectedCount);
