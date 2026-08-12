using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Ai;

public sealed class AiAnalysisService(
    IFinanceServiceClient financeServiceClient,
    IDebtServiceClient debtServiceClient,
    IAiAnalysisProvider provider)
{
    public async Task<AiAnalysisResponse> AnalyzeAsync(
        string? month,
        CancellationToken cancellationToken)
    {
        var financeSummaryTask = financeServiceClient.GetMonthlySummaryAsync(month, cancellationToken);
        var budgetTask = financeServiceClient.GetMonthlyBudgetAsync(month, cancellationToken);
        var trendTask = financeServiceClient.GetTrendAsync(month, 6, cancellationToken);
        var goalsTask = financeServiceClient.GetFinancialGoalsAsync(cancellationToken);
        var projectionTask = financeServiceClient.GetCashFlowProjectionAsync(6, cancellationToken);
        var expensesTask = financeServiceClient.GetExpensesAsync(cancellationToken);
        var debtContextTask = debtServiceClient.GetAnalysisContextAsync(cancellationToken);
        var settlementsTask = debtServiceClient.GetSimplifiedSettlementsAsync(null, cancellationToken);

        await Task.WhenAll(
            financeSummaryTask,
            budgetTask,
            trendTask,
            goalsTask,
            projectionTask,
            expensesTask,
            debtContextTask,
            settlementsTask);

        var financeSummary = await financeSummaryTask;
        var budget = await budgetTask;
        var trend = await trendTask;
        var goals = await goalsTask;
        var projection = await projectionTask;
        var expenses = await expensesTask;
        var debtContext = await debtContextTask;
        var settlements = await settlementsTask;
        var expenseCategories = expenses
            .Where(expense => expense.TransactionDate.ToString("yyyy-MM") == financeSummary.ReferenceMonth)
            .GroupBy(expense => expense.Category)
            .Select(group => new AiCategoryContext(group.Key, group.Sum(expense => expense.Amount)))
            .OrderByDescending(category => category.Amount)
            .ToList();

        var groupAliases = debtContext.Groups
            .Where(group => group.GroupId.HasValue)
            .Select((group, index) => new
            {
                GroupId = group.GroupId!.Value,
                Alias = $"Grupo {index + 1}"
            })
            .ToDictionary(group => group.GroupId, group => group.Alias);

        var sanitizedContext = new AiAnalysisContext(
            financeSummary.ReferenceMonth,
            financeSummary.TotalIncome,
            financeSummary.TotalExpenses,
            financeSummary.Balance,
            expenseCategories,
            debtContext.TotalOwed,
            debtContext.TotalToReceive,
            debtContext.OpenDebtsCount,
            debtContext.PaidDebtsCount,
            debtContext.OverdueDebtsCount,
            debtContext.DueSoonDebtsCount,
            debtContext.Categories
                .Select(category => new AiDebtCategoryContext(
                    category.Category,
                    category.TotalOwed,
                    category.TotalToReceive,
                    category.OpenDebtsCount))
                .ToList(),
            debtContext.Groups
                .Select(group => new AiDebtGroupContext(
                    group.GroupId.HasValue
                        ? groupAliases[group.GroupId.Value]
                        : "Sem grupo",
                    group.TotalOwed,
                    group.TotalToReceive,
                    group.OpenDebtsCount))
                .ToList(),
            debtContext.TopDrivers
                .Select(driver => new AiDebtDriverContext(
                    driver.Category,
                    driver.GroupId.HasValue
                        ? groupAliases.GetValueOrDefault(driver.GroupId.Value, "Grupo")
                        : "Sem grupo",
                    driver.TotalOwed,
                    driver.TotalToReceive,
                    driver.DueDate,
                    driver.IsOverdue))
                .ToList(),
            settlements.OriginalTransferCount,
            settlements.SimplifiedTransferCount,
            budget.Categories
                .Where(category => category.Planned > 0m)
                .Select(category => new AiBudgetCategoryContext(
                    category.Category,
                    category.Planned,
                    category.Spent,
                    category.Remaining,
                    category.UsagePercentage))
                .ToList(),
            trend.Items
                .Select(item => new AiMonthlyTrendContext(
                    item.ReferenceMonth,
                    item.TotalIncome,
                    item.TotalExpenses,
                    item.Balance))
                .ToList(),
            goals.Select((goal, index) => new AiGoalContext(
                    $"Meta {index + 1}",
                    goal.TargetAmount,
                    goal.CurrentAmount,
                    goal.RemainingAmount,
                    goal.ProgressPercentage,
                    goal.TargetDate,
                    goal.Status,
                    goal.RequiredMonthlyContribution))
                .ToList(),
            new AiCashFlowProjectionContext(
                projection.ProjectedCumulativeBalance,
                projection.Items.Select(item => new AiProjectionMonthContext(
                    item.ReferenceMonth,
                    item.ProjectedIncome,
                    item.ProjectedExpenses,
                    item.ProjectedNet,
                    item.CumulativeBalance)).ToList()));

        return await provider.AnalyzeAsync(sanitizedContext, cancellationToken);
    }
}
