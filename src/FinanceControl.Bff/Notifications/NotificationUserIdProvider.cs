using FinanceControl.Bff.Auth;
using Microsoft.AspNetCore.SignalR;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User is null
            ? null
            : AuthenticatedUser.GetId(connection.User).ToString();
}
