using FinanceControl.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationPreferenceService(
    BffDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<NotificationPreferencesResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .ToDictionaryAsync(preference => preference.Type, cancellationToken);

        return new NotificationPreferencesResponse(NotificationTypeCatalog.All
            .Select(definition =>
            {
                stored.TryGetValue(definition.Type, out var preference);
                return new NotificationPreferenceItemResponse(
                    definition.ContractValue,
                    definition.Category,
                    definition.Label,
                    preference?.InAppEnabled ?? true,
                    preference?.PushEnabled ?? true,
                    preference?.EmailEnabled ?? false);
            })
            .ToList());
    }

    public async Task<NotificationPreferencesResponse> UpdateAsync(
        Guid userId,
        IReadOnlyDictionary<NotificationType, UpdateNotificationPreferenceItemRequest> updates,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.NotificationPreferences
            .Where(preference => preference.UserId == userId && updates.Keys.Contains(preference.Type))
            .ToDictionaryAsync(preference => preference.Type, cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var (type, update) in updates)
        {
            if (stored.TryGetValue(type, out var preference))
            {
                preference.Update(
                    update.InAppEnabled,
                    update.PushEnabled,
                    update.EmailEnabled,
                    now);
            }
            else
            {
                dbContext.NotificationPreferences.Add(new NotificationPreference(
                    userId,
                    type,
                    update.InAppEnabled,
                    update.PushEnabled,
                    update.EmailEnabled,
                    now));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }
}
