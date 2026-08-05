using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceControl.Bff.Notifications;

[Authorize]
public sealed class NotificationHub : Hub;
