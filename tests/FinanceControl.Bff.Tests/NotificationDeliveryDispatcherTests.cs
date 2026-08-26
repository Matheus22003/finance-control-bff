using System.Text.Json;
using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Tests;

public sealed class NotificationDeliveryDispatcherTests
{
    [Fact]
    public void CreatePushPayload_UsesTheAngularServiceWorkerContract()
    {
        var notification = new UserNotification(
            Guid.NewGuid(),
            NotificationType.PaymentConfirmed,
            "Pagamento confirmado",
            "Seu pagamento foi confirmado.",
            "/debts",
            DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(
            NotificationDeliveryDispatcher.CreatePushPayload(notification));
        var payload = document.RootElement.GetProperty("notification");

        Assert.Equal("Pagamento confirmado", payload.GetProperty("title").GetString());
        Assert.Equal("Seu pagamento foi confirmado.", payload.GetProperty("body").GetString());
        Assert.False(payload.TryGetProperty("options", out _));
        var data = payload.GetProperty("data");
        Assert.Equal("/debts", data.GetProperty("route").GetString());
        Assert.Equal("PAYMENT_CONFIRMED", data.GetProperty("type").GetString());
        var defaultAction = data
            .GetProperty("onActionClick")
            .GetProperty("default");
        Assert.Equal(
            "navigateLastFocusedOrOpen",
            defaultAction.GetProperty("operation").GetString());
        Assert.Equal("/debts", defaultAction.GetProperty("url").GetString());
    }
}
