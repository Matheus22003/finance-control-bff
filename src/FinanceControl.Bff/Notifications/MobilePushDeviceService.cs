using FinanceControl.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Notifications;

/// <summary>
/// Registers mobile Expo tokens only. Sending is intentionally a separate server-side boundary:
/// a future provider adapter may read active tokens from this service and call Expo with server
/// credentials; this service does not make provider calls during device registration.
/// </summary>
public sealed class MobilePushDeviceService(
    BffDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<MobilePushDeviceResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var devices = await dbContext.MobilePushDevices
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.UpdatedAt)
            .ToListAsync(cancellationToken);

        return devices.Select(ToResponse).ToList();
    }

    public async Task<MobilePushDeviceResponse> UpsertAsync(
        Guid userId,
        UpsertMobilePushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var installationId = Guid.TryParse(request.InstallationId, out var parsedInstallationId)
            ? parsedInstallationId.ToString("D")
            : request.InstallationId.Trim();
        var platform = UserMobilePushDevice.NormalizePlatform(request.Platform);
        var device = await dbContext.MobilePushDevices.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId &&
                         candidate.InstallationId == installationId &&
                         candidate.Platform == platform,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (device is null)
        {
            device = new UserMobilePushDevice(
                userId,
                installationId,
                platform,
                request.ExpoPushToken,
                request.DeviceName,
                request.AppVersion,
                now);
            dbContext.MobilePushDevices.Add(device);
        }
        else
        {
            device.Update(request.ExpoPushToken, request.DeviceName, request.AppVersion, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(device);
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        string installationId,
        string platform,
        CancellationToken cancellationToken)
    {
        var normalizedPlatform = UserMobilePushDevice.NormalizePlatform(platform);
        var device = await dbContext.MobilePushDevices.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId &&
                         candidate.InstallationId == installationId.Trim() &&
                         candidate.Platform == normalizedPlatform,
            cancellationToken);
        if (device is null)
        {
            return false;
        }

        dbContext.MobilePushDevices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static MobilePushDeviceResponse ToResponse(UserMobilePushDevice device) => new(
        device.Id,
        device.InstallationId,
        device.Platform,
        device.DeviceName,
        device.AppVersion,
        device.CreatedAt,
        device.UpdatedAt);
}
