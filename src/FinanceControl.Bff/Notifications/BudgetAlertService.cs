using FinanceControl.Bff.Clients.Finance;

namespace FinanceControl.Bff.Notifications;

public sealed class BudgetAlertService(NotificationService notificationService)
{
    public async Task PublishCrossingAsync(
        Guid userId,
        MonthlyBudgetResponse before,
        MonthlyBudgetResponse after,
        string category,
        CancellationToken cancellationToken)
    {
        var previous = before.Categories.SingleOrDefault(item =>
            string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase));
        var current = after.Categories.SingleOrDefault(item =>
            string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase));
        if (current is null || current.Planned <= 0m)
        {
            return;
        }

        var previousUsage = previous is null || previous.Planned <= 0m
            ? 0m
            : previous.UsagePercentage;
        var type = current.UsagePercentage >= 100m && previousUsage < 100m
            ? NotificationType.BudgetExceeded
            : current.UsagePercentage >= 80m && previousUsage < 80m
                ? NotificationType.BudgetWarning
                : (NotificationType?)null;
        if (type is null)
        {
            return;
        }

        var categoryLabel = current.Name;
        var title = type == NotificationType.BudgetExceeded
            ? $"Orçamento de {categoryLabel} excedido"
            : $"Orçamento de {categoryLabel} em atenção";
        var message = type == NotificationType.BudgetExceeded
            ? $"Você consumiu {current.UsagePercentage:N0}% do limite de {categoryLabel}."
            : $"Você já consumiu {current.UsagePercentage:N0}% do limite de {categoryLabel}.";

        await notificationService.PublishOnceAsync(
            [userId],
            type.Value,
            title,
            message,
            "/finance",
            DeduplicationKey(after.ReferenceMonth, current.Category, type.Value),
            cancellationToken);
    }

    public async Task<int> PublishCurrentStateAsync(
        Guid userId,
        MonthlyBudgetResponse budget,
        CancellationToken cancellationToken)
    {
        var createdCount = 0;
        foreach (var category in budget.Categories.Where(item => item.Planned > 0m))
        {
            var type = category.UsagePercentage >= 100m
                ? NotificationType.BudgetExceeded
                : category.UsagePercentage >= 80m
                    ? NotificationType.BudgetWarning
                    : (NotificationType?)null;
            if (type is null)
            {
                continue;
            }

            var categoryLabel = category.Name;
            var title = type == NotificationType.BudgetExceeded
                ? $"Orçamento de {categoryLabel} excedido"
                : $"Orçamento de {categoryLabel} em atenção";
            var message = type == NotificationType.BudgetExceeded
                ? $"Você consumiu {category.UsagePercentage:N0}% do limite de {categoryLabel}."
                : $"Você já consumiu {category.UsagePercentage:N0}% do limite de {categoryLabel}.";
            createdCount += await notificationService.PublishOnceAsync(
                [userId],
                type.Value,
                title,
                message,
                "/finance",
                DeduplicationKey(budget.ReferenceMonth, category.Category, type.Value),
                cancellationToken);
        }

        return createdCount;
    }

    private static string DeduplicationKey(
        string referenceMonth,
        string category,
        NotificationType type) =>
        $"budget:{referenceMonth}:{category.ToUpperInvariant()}:{type}";

}
