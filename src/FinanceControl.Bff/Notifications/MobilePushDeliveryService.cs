using System.Net.Http.Json;
using System.Text.Json;
using FinanceControl.Bff.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Notifications;

public sealed class MobilePushDeliveryService(
    BffDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<MobilePushOptions> options,
    ILogger<MobilePushDeliveryService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MobilePushSendResponse> SendToUserAsync(
        Guid userId,
        MobilePushMessage message,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new MobilePushSendResponse(false, 0, 0, 0);
        }

        var devices = await dbContext.MobilePushDevices
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return new MobilePushSendResponse(true, 0, 0, 0);
        }

        var payloads = devices.Select(device => new
        {
            to = device.ExpoPushToken,
            title = message.Title,
            body = message.Body,
            sound = "default",
            data = new
            {
                route = message.Route,
                notificationId = message.NotificationId,
                type = message.Type
            }
        }).ToArray();

        try
        {
            using var response = await httpClientFactory.CreateClient(nameof(MobilePushDeliveryService))
                .PostAsJsonAsync(settings.ExpoApiUrl, payloads, JsonOptions, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<ExpoPushResponse>(
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode || result?.Data is null)
            {
                logger.LogWarning(
                    "Mobile push provider returned HTTP {StatusCode} for {DeviceCount} devices.",
                    (int)response.StatusCode,
                    devices.Count);
                return new MobilePushSendResponse(true, devices.Count, 0, devices.Count);
            }

            var accepted = result.Data.Count(ticket =>
                string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase));
            return new MobilePushSendResponse(true, devices.Count, accepted, result.Data.Count - accepted);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Mobile push provider timed out for {DeviceCount} devices.", devices.Count);
            return new MobilePushSendResponse(true, devices.Count, 0, devices.Count);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Mobile push provider request failed.");
            return new MobilePushSendResponse(true, devices.Count, 0, devices.Count);
        }
    }

    private sealed record ExpoPushResponse(IReadOnlyList<ExpoPushTicket>? Data);

    private sealed record ExpoPushTicket(string? Status, string? Id, ExpoPushError? Details);

    private sealed record ExpoPushError(string? Error, string? Message);
}
