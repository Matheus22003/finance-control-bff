using FinanceControl.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Notifications;

public sealed class PushSubscriptionService(
    BffDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<PushSubscriptionResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.PushSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.UserId == userId)
            .OrderByDescending(subscription => subscription.UpdatedAt)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(ToResponse).ToList();
    }

    public async Task<PushSubscriptionResponse> UpsertAsync(
        Guid userId,
        CreatePushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var endpointHash = UserPushSubscription.HashEndpoint(request.Endpoint);
        var subscription = await dbContext.PushSubscriptions.SingleOrDefaultAsync(
            candidate => candidate.EndpointHash == endpointHash,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (subscription is null)
        {
            subscription = new UserPushSubscription(
                userId,
                request.Endpoint,
                request.P256Dh,
                request.Auth,
                request.DeviceName,
                now);
            dbContext.PushSubscriptions.Add(subscription);
        }
        else if (subscription.UserId == userId)
        {
            subscription.Update(
                request.Endpoint,
                request.P256Dh,
                request.Auth,
                request.DeviceName,
                now);
        }
        else
        {
            dbContext.PushSubscriptions.Remove(subscription);
            subscription = new UserPushSubscription(
                userId,
                request.Endpoint,
                request.P256Dh,
                request.Auth,
                request.DeviceName,
                now);
            dbContext.PushSubscriptions.Add(subscription);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(subscription);
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.PushSubscriptions.SingleOrDefaultAsync(
            candidate => candidate.Id == subscriptionId && candidate.UserId == userId,
            cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        dbContext.PushSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveByEndpointAsync(
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var endpointHash = UserPushSubscription.HashEndpoint(endpoint);
        var subscription = await dbContext.PushSubscriptions.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId && candidate.EndpointHash == endpointHash,
            cancellationToken);
        if (subscription is null)
        {
            return;
        }

        dbContext.PushSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PushSubscriptionResponse ToResponse(UserPushSubscription subscription) => new(
        subscription.Id,
        subscription.DeviceName,
        subscription.CreatedAt,
        subscription.UpdatedAt);
}
