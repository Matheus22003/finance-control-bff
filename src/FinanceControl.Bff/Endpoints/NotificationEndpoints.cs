using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        var notifications = group.MapGroup("/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

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

        return group;
    }
}
