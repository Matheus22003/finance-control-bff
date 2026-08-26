using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationService(
    BffDbContext dbContext,
    IHubContext<NotificationHub> hubContext,
    NotificationDeliveryDispatcher deliveryDispatcher,
    TimeProvider timeProvider,
    ILogger<NotificationService> logger)
{
    public const string ReceivedEvent = "notificationReceived";

    public async Task<IReadOnlyList<NotificationResponse>> GetAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && notification.IsVisibleInApp);
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(notification => ToResponse(notification))
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(
            notification => notification.UserId == userId && notification.IsVisibleInApp && !notification.IsRead,
            cancellationToken);

    public async Task<NotificationResponse?> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            candidate => candidate.Id == notificationId &&
                         candidate.UserId == userId &&
                         candidate.IsVisibleInApp,
            cancellationToken);
        if (notification is null)
        {
            return null;
        }

        notification.MarkAsRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(notification);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await dbContext.Notifications
            .Where(notification =>
                notification.UserId == userId && notification.IsVisibleInApp && !notification.IsRead)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var notification in notifications)
        {
            notification.MarkAsRead(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }

    public async Task PublishAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string title,
        string message,
        string? route,
        CancellationToken cancellationToken)
    {
        await PublishCoreAsync(
            recipientUserIds,
            type,
            title,
            message,
            route,
            null,
            cancellationToken);
    }

    public Task<int> PublishOnceAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string title,
        string message,
        string? route,
        string deduplicationKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);
        return PublishCoreAsync(
            recipientUserIds,
            type,
            title,
            message,
            route,
            deduplicationKey.Trim(),
            cancellationToken);
    }

    private async Task<int> PublishCoreAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string title,
        string message,
        string? route,
        string? deduplicationKey,
        CancellationToken cancellationToken)
    {
        var requestedRecipients = recipientUserIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();
        if (requestedRecipients.Count == 0)
        {
            return 0;
        }

        var recipientSettings = await dbContext.Users
            .AsNoTracking()
            .Where(user => requestedRecipients.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.PushNotificationsEnabled,
                user.EmailNotificationsEnabled
            })
            .ToListAsync(cancellationToken);
        if (recipientSettings.Count == 0)
        {
            return 0;
        }

        var preferences = await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(preference =>
                requestedRecipients.Contains(preference.UserId) && preference.Type == type)
            .ToDictionaryAsync(preference => preference.UserId, cancellationToken);
        var deliverySettings = recipientSettings
            .Select(user =>
            {
                preferences.TryGetValue(user.Id, out var preference);
                return new
                {
                    UserId = user.Id,
                    InAppEnabled = preference?.InAppEnabled ?? true,
                    PushEnabled = user.PushNotificationsEnabled && (preference?.PushEnabled ?? true),
                    EmailEnabled = user.EmailNotificationsEnabled && (preference?.EmailEnabled ?? false)
                };
            })
            .Where(settings => settings.InAppEnabled || settings.PushEnabled || settings.EmailEnabled)
            .ToList();
        var recipients = deliverySettings.Select(settings => settings.UserId).ToList();

        if (deduplicationKey is not null)
        {
            var alreadyNotified = await dbContext.Notifications
                .AsNoTracking()
                .Where(notification =>
                    recipients.Contains(notification.UserId) &&
                    notification.DeduplicationKey == deduplicationKey)
                .Select(notification => notification.UserId)
                .ToListAsync(cancellationToken);
            recipients = recipients.Except(alreadyNotified).ToList();
            if (recipients.Count == 0)
            {
                return 0;
            }
        }

        var now = timeProvider.GetUtcNow();
        var notifications = deliverySettings
            .Where(settings => recipients.Contains(settings.UserId))
            .Select(settings => new UserNotification(
                settings.UserId,
                type,
                title,
                message,
                route,
                now,
                deduplicationKey,
                settings.InAppEnabled))
            .ToList();
        dbContext.Notifications.AddRange(notifications);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            if (!notification.IsVisibleInApp)
            {
                continue;
            }

            try
            {
                await hubContext.Clients
                    .User(notification.UserId.ToString())
                    .SendAsync(ReceivedEvent, ToResponse(notification), cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Notification {NotificationId} was persisted but could not be delivered in real time.",
                    notification.Id);
            }
        }

        await deliveryDispatcher.DispatchAsync(notifications, cancellationToken);

        return notifications.Count;
    }

    private static NotificationResponse ToResponse(UserNotification notification) => new(
        notification.Id,
        NotificationTypeCatalog.ToContractValue(notification.Type),
        notification.Title,
        notification.Message,
        notification.Route,
        notification.IsRead,
        notification.ReadAt,
        notification.CreatedAt);
}
