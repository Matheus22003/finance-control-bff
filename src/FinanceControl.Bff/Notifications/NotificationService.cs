using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationService(
    BffDbContext dbContext,
    IHubContext<NotificationHub> hubContext,
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
            .Where(notification => notification.UserId == userId);
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
            notification => notification.UserId == userId && !notification.IsRead,
            cancellationToken);

    public async Task<NotificationResponse?> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            candidate => candidate.Id == notificationId && candidate.UserId == userId,
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
            .Where(notification => notification.UserId == userId && !notification.IsRead)
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

        var recipients = await dbContext.Users
            .AsNoTracking()
            .Where(user => requestedRecipients.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return 0;
        }

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
        var notifications = recipients
            .Select(userId => new UserNotification(
                userId,
                type,
                title,
                message,
                route,
                now,
                deduplicationKey))
            .ToList();
        dbContext.Notifications.AddRange(notifications);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
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

        return notifications.Count;
    }

    private static NotificationResponse ToResponse(UserNotification notification) => new(
        notification.Id,
        ToContractValue(notification.Type),
        notification.Title,
        notification.Message,
        notification.Route,
        notification.IsRead,
        notification.ReadAt,
        notification.CreatedAt);

    private static string ToContractValue(NotificationType type) =>
        string.Concat(type.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $"_{character}"
                : character.ToString())).ToUpperInvariant();
}
