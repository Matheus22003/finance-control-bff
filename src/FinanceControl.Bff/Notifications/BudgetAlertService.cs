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

        var categoryLabel = CategoryLabel(current.Category);
        var title = type == NotificationType.BudgetExceeded
            ? $"Orçamento de {categoryLabel} excedido"
            : $"Orçamento de {categoryLabel} em atenção";
        var message = type == NotificationType.BudgetExceeded
            ? $"Você consumiu {current.UsagePercentage:N0}% do limite de {categoryLabel}."
            : $"Você já consumiu {current.UsagePercentage:N0}% do limite de {categoryLabel}.";

        await notificationService.PublishAsync(
            [userId],
            type.Value,
            title,
            message,
            "/finance",
            cancellationToken);
    }

    private static string CategoryLabel(string category) => category.ToUpperInvariant() switch
    {
        "FOOD" => "alimentação",
        "TRANSPORT" => "transporte",
        "RENT" => "moradia",
        "LEISURE" => "lazer",
        "HEALTH" => "saúde",
        _ => "outros"
    };
}
