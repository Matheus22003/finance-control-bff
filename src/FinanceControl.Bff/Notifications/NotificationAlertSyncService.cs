using FinanceControl.Bff.Clients.Finance;

namespace FinanceControl.Bff.Notifications;

public sealed class NotificationAlertSyncService(
    IFinanceServiceClient financeServiceClient,
    GoalAlertService goalAlertService,
    BudgetAlertService budgetAlertService,
    TimeProvider timeProvider)
{
    public async Task<NotificationSyncResponse> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var goalsTask = financeServiceClient.GetFinancialGoalsAsync(cancellationToken);
        var budgetTask = financeServiceClient.GetMonthlyBudgetAsync(null, cancellationToken);
        await Task.WhenAll(goalsTask, budgetTask);

        var createdCount = await goalAlertService.PublishCurrentStateAsync(
            userId,
            await goalsTask,
            cancellationToken);
        createdCount += await budgetAlertService.PublishCurrentStateAsync(
            userId,
            await budgetTask,
            cancellationToken);

        return new NotificationSyncResponse(
            createdCount,
            timeProvider.GetUtcNow());
    }
}
