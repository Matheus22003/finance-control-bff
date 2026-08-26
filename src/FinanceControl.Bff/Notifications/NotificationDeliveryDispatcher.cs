using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using FinanceControl.Bff.Email;
using FinanceControl.Bff.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationDeliveryDispatcher(
    BffDbContext dbContext,
    WebPushClient webPushClient,
    IOptions<WebPushOptions> webPushOptions,
    IApplicationEmailSender emailSender,
    ILogger<NotificationDeliveryDispatcher> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(
        IReadOnlyList<UserNotification> notifications,
        CancellationToken cancellationToken)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        var userIds = notifications.Select(notification => notification.UserId).Distinct().ToList();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.PushNotificationsEnabled,
                user.EmailNotificationsEnabled
            })
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var preferences = await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(preference => userIds.Contains(preference.UserId))
            .ToDictionaryAsync(
                preference => (preference.UserId, preference.Type),
                cancellationToken);
        var subscriptions = await dbContext.PushSubscriptions
            .Where(subscription => userIds.Contains(subscription.UserId))
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            if (!users.TryGetValue(notification.UserId, out var user))
            {
                continue;
            }

            preferences.TryGetValue((notification.UserId, notification.Type), out var preference);
            var pushEnabled = user.PushNotificationsEnabled && (preference?.PushEnabled ?? true);
            if (pushEnabled && webPushOptions.Value.IsConfigured)
            {
                await SendPushAsync(
                    notification,
                    subscriptions.Where(candidate => candidate.UserId == notification.UserId).ToList(),
                    cancellationToken);
            }

            var emailEnabled = user.EmailNotificationsEnabled && (preference?.EmailEnabled ?? false);
            if (emailEnabled && !string.IsNullOrWhiteSpace(user.Email))
            {
                await SendEmailAsync(
                    notification,
                    user.Email,
                    user.DisplayName,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendPushAsync(
        UserNotification notification,
        IReadOnlyList<UserPushSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        var options = webPushOptions.Value;
        var payload = CreatePushPayload(notification);
        var vapidDetails = new VapidDetails(options.Subject, options.PublicKey, options.PrivateKey);

        foreach (var storedSubscription in subscriptions)
        {
            try
            {
                var subscription = new PushSubscription(
                    storedSubscription.Endpoint,
                    storedSubscription.P256Dh,
                    storedSubscription.Auth);
                await webPushClient.SendNotificationAsync(
                    subscription,
                    payload,
                    vapidDetails,
                    cancellationToken);
            }
            catch (WebPushException exception) when (
                exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                dbContext.PushSubscriptions.Remove(storedSubscription);
                logger.LogInformation(
                    "Removed expired push subscription {SubscriptionId}.",
                    storedSubscription.Id);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Push delivery failed for notification {NotificationId} and subscription {SubscriptionId}.",
                    notification.Id,
                    storedSubscription.Id);
            }
        }
    }

    internal static string CreatePushPayload(UserNotification notification)
    {
        var route = notification.Route ?? "/dashboard";
        return JsonSerializer.Serialize(new
        {
            notification = new
            {
                title = notification.Title,
                body = notification.Message,
                icon = "/icon-192.png",
                badge = "/icon-192.png",
                tag = $"finance-control-{notification.Id:N}",
                data = new
                {
                    route,
                    notificationId = notification.Id,
                    type = NotificationTypeCatalog.ToContractValue(notification.Type),
                    onActionClick = new
                    {
                        @default = new
                        {
                            operation = "navigateLastFocusedOrOpen",
                            url = route
                        }
                    }
                }
            }
        }, JsonOptions);
    }

    private async Task SendEmailAsync(
        UserNotification notification,
        string email,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            var title = HtmlEncoder.Default.Encode(notification.Title);
            var message = HtmlEncoder.Default.Encode(notification.Message);
            await emailSender.SendAsync(
                email,
                displayName,
                $"{notification.Title} | Finance Control",
                $"<h1>{title}</h1><p>{message}</p>",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Email delivery failed for notification {NotificationId}.",
                notification.Id);
        }
    }
}
