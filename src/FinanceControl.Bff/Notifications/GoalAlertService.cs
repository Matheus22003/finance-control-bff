using FinanceControl.Bff.Clients.Finance;

namespace FinanceControl.Bff.Notifications;

public sealed class GoalAlertService(
    NotificationService notificationService,
    TimeProvider timeProvider)
{
    private const int DueSoonDays = 30;

    public async Task<int> PublishCurrentStateAsync(
        Guid userId,
        IEnumerable<FinancialGoalResponse> goals,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var createdCount = 0;
        foreach (var goal in goals)
        {
            var alert = CreateAlert(goal, today);
            if (alert is null)
            {
                continue;
            }

            createdCount += await notificationService.PublishOnceAsync(
                [userId],
                alert.Type,
                alert.Title,
                alert.Message,
                "/finance",
                alert.DeduplicationKey,
                cancellationToken);
        }

        return createdCount;
    }

    private static GoalAlert? CreateAlert(FinancialGoalResponse goal, DateOnly today)
    {
        if (string.Equals(goal.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            return new GoalAlert(
                NotificationType.GoalCompleted,
                "Meta concluída",
                $"Parabéns! Você atingiu a meta \"{goal.Name}\".",
                $"goal:{goal.Id}:completed");
        }

        if (string.Equals(goal.Status, "OVERDUE", StringComparison.OrdinalIgnoreCase) ||
            goal.TargetDate < today)
        {
            return new GoalAlert(
                NotificationType.GoalOverdue,
                "Meta com prazo vencido",
                $"A meta \"{goal.Name}\" venceu e ainda faltam {Money(goal.RemainingAmount)}.",
                $"goal:{goal.Id}:overdue:{goal.TargetDate:yyyy-MM-dd}");
        }

        var daysRemaining = goal.TargetDate.DayNumber - today.DayNumber;
        if (daysRemaining is < 0 or > DueSoonDays)
        {
            return null;
        }

        var deadline = daysRemaining switch
        {
            0 => "vence hoje",
            1 => "vence amanhã",
            _ => $"vence em {daysRemaining} dias"
        };
        return new GoalAlert(
            NotificationType.GoalDueSoon,
            "Meta próxima do prazo",
            $"A meta \"{goal.Name}\" {deadline} e ainda faltam {Money(goal.RemainingAmount)}.",
            $"goal:{goal.Id}:due-soon:{goal.TargetDate:yyyy-MM-dd}");
    }

    private static string Money(decimal value) =>
        value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private sealed record GoalAlert(
        NotificationType Type,
        string Title,
        string Message,
        string DeduplicationKey);
}
