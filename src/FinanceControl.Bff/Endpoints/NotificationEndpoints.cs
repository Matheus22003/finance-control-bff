using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Notifications;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        var notifications = group.MapGroup("/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        notifications.MapPost("/sync", async (
                HttpContext context,
                NotificationAlertSyncService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.SyncAsync(
                    AuthenticatedUser.GetId(context.User),
                    cancellationToken)))
            .WithName("SyncNotificationAlerts")
            .Produces<NotificationSyncResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        notifications.MapGet("/", async (
                HttpContext context,
                bool? unreadOnly,
                int? limit,
                NotificationService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(
                    AuthenticatedUser.GetId(context.User),
                    unreadOnly ?? false,
                    limit ?? 30,
                    cancellationToken)))
            .WithName("GetNotifications")
            .Produces<IReadOnlyList<NotificationResponse>>();

        notifications.MapGet("/unread-count", async (
                HttpContext context,
                NotificationService service,
                CancellationToken cancellationToken) =>
                Results.Ok(new NotificationUnreadCountResponse(
                    await service.GetUnreadCountAsync(
                        AuthenticatedUser.GetId(context.User),
                        cancellationToken))))
            .WithName("GetUnreadNotificationCount")
            .Produces<NotificationUnreadCountResponse>();

        notifications.MapPost("/{notificationId:guid}/read", async (
                Guid notificationId,
                HttpContext context,
                NotificationService service,
                CancellationToken cancellationToken) =>
            {
                var notification = await service.MarkAsReadAsync(
                    AuthenticatedUser.GetId(context.User),
                    notificationId,
                    cancellationToken);
                return notification is null
                    ? Results.NotFound()
                    : Results.Ok(notification);
            })
            .WithName("MarkNotificationAsRead")
            .Produces<NotificationResponse>()
            .Produces(StatusCodes.Status404NotFound);

        notifications.MapPost("/read-all", async (
                HttpContext context,
                NotificationService service,
                CancellationToken cancellationToken) =>
            {
                await service.MarkAllAsReadAsync(
                    AuthenticatedUser.GetId(context.User),
                    cancellationToken);
                return Results.Ok(new NotificationUnreadCountResponse(0));
            })
            .WithName("MarkAllNotificationsAsRead")
            .Produces<NotificationUnreadCountResponse>();

        notifications.MapGet("/preferences", async (
                HttpContext context,
                NotificationPreferenceService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(
                    AuthenticatedUser.GetId(context.User),
                    cancellationToken)))
            .WithName("GetNotificationPreferences")
            .Produces<NotificationPreferencesResponse>();

        notifications.MapPut("/preferences", async (
                UpdateNotificationPreferencesRequest request,
                HttpContext context,
                NotificationPreferenceService service,
                CancellationToken cancellationToken) =>
            {
                if (request.Preferences is null || request.Preferences.Count == 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["preferences"] = ["At least one notification preference is required."]
                    });
                }

                var updates = new Dictionary<NotificationType, UpdateNotificationPreferenceItemRequest>();
                foreach (var preference in request.Preferences)
                {
                    if (!NotificationTypeCatalog.TryParse(preference.Type, out var type))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["preferences"] = [$"Unsupported notification type: {preference.Type}."]
                        });
                    }

                    if (!updates.TryAdd(type, preference))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["preferences"] = [$"Duplicate notification type: {preference.Type}."]
                        });
                    }
                }

                return Results.Ok(await service.UpdateAsync(
                    AuthenticatedUser.GetId(context.User),
                    updates,
                    cancellationToken));
            })
            .WithName("UpdateNotificationPreferences")
            .Produces<NotificationPreferencesResponse>()
            .ProducesValidationProblem();

        var push = notifications.MapGroup("/push");

        push.MapGet("/configuration", (IOptions<WebPushOptions> options) =>
                Results.Ok(new PushNotificationConfigurationResponse(
                    options.Value.IsConfigured,
                    options.Value.IsConfigured ? options.Value.PublicKey : null)))
            .WithName("GetPushNotificationConfiguration")
            .Produces<PushNotificationConfigurationResponse>();

        push.MapGet("/subscriptions", async (
                HttpContext context,
                PushSubscriptionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(
                    AuthenticatedUser.GetId(context.User),
                    cancellationToken)))
            .WithName("GetPushSubscriptions")
            .Produces<IReadOnlyList<PushSubscriptionResponse>>();

        push.MapPost("/subscriptions", async (
                CreatePushSubscriptionRequest request,
                HttpContext context,
                PushSubscriptionService service,
                CancellationToken cancellationToken) =>
            {
                var errors = ValidatePushSubscription(request);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                return Results.Ok(await service.UpsertAsync(
                    AuthenticatedUser.GetId(context.User),
                    request,
                    cancellationToken));
            })
            .WithName("CreatePushSubscription")
            .Produces<PushSubscriptionResponse>()
            .ProducesValidationProblem();

        push.MapDelete("/subscriptions/{subscriptionId:guid}", async (
                Guid subscriptionId,
                HttpContext context,
                PushSubscriptionService service,
                CancellationToken cancellationToken) =>
                await service.RemoveAsync(
                    AuthenticatedUser.GetId(context.User),
                    subscriptionId,
                    cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound())
            .WithName("DeletePushSubscription")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        push.MapPost("/subscriptions/unsubscribe", async (
                RemovePushSubscriptionRequest request,
                HttpContext context,
                PushSubscriptionService service,
                CancellationToken cancellationToken) =>
            {
                if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) ||
                    endpoint.Scheme != Uri.UriSchemeHttps)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["endpoint"] = ["A valid HTTPS push endpoint is required."]
                    });
                }

                await service.RemoveByEndpointAsync(
                    AuthenticatedUser.GetId(context.User),
                    request.Endpoint,
                    cancellationToken);
                return Results.NoContent();
            })
            .WithName("UnsubscribeCurrentPushSubscription")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        return group;
    }

    private static Dictionary<string, string[]> ValidatePushSubscription(
        CreatePushSubscriptionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            request.Endpoint.Length > 2048)
        {
            errors["endpoint"] = ["A valid HTTPS push endpoint with at most 2048 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(request.P256Dh) || request.P256Dh.Length > 512)
        {
            errors["p256Dh"] = ["A valid P-256 public key is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 512)
        {
            errors["auth"] = ["A valid push authentication secret is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Trim().Length > 200)
        {
            errors["deviceName"] = ["Device name is required and must contain at most 200 characters."];
        }

        return errors;
    }
}
