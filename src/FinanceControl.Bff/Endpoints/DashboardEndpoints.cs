using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Dashboard;

namespace FinanceControl.Bff.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", GetDashboard)
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .Produces<DashboardResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetDashboard(
        IFinanceServiceClient financeServiceClient,
        IDebtServiceClient debtServiceClient,
        CancellationToken cancellationToken)
    {
        var financeSummaryTask = financeServiceClient.GetMonthlySummaryAsync(null, cancellationToken);
        var financeTrendTask = financeServiceClient.GetTrendAsync(null, 6, cancellationToken);
        var budgetTask = financeServiceClient.GetMonthlyBudgetAsync(null, cancellationToken);
        var goalsTask = financeServiceClient.GetFinancialGoalsAsync(cancellationToken);
        var projectionTask = financeServiceClient.GetCashFlowProjectionAsync(6, cancellationToken);
        var debtSummaryTask = debtServiceClient.GetSummaryAsync(cancellationToken);

        await Task.WhenAll(
            financeSummaryTask,
            financeTrendTask,
            budgetTask,
            goalsTask,
            projectionTask,
            debtSummaryTask);

        var financeSummary = await financeSummaryTask;
        var financeTrend = await financeTrendTask;
        var budget = await budgetTask;
        var goals = await goalsTask;
        var projection = await projectionTask;
        var debtSummary = await debtSummaryTask;

        return Results.Ok(new DashboardResponse
        {
            Balance = financeSummary.Balance,
            TotalIncome = financeSummary.TotalIncome,
            TotalExpenses = financeSummary.TotalExpenses,
            DebtsSummary = new DebtsSummary
            {
                TotalOwed = debtSummary.TotalOwed,
                TotalToReceive = debtSummary.TotalToReceive,
                OpenDebtsCount = debtSummary.OpenDebtsCount
            },
            Budget = new DashboardBudgetSummary(
                budget.ReferenceMonth,
                budget.TotalPlanned,
                budget.TotalSpent,
                budget.TotalRemaining,
                budget.Categories.Select(category => new DashboardBudgetCategory(
                    category.Category,
                    category.Name,
                    category.Planned,
                    category.Spent,
                    category.Remaining,
                    category.UsagePercentage)).ToList()),
            MonthlyTrend = financeTrend.Items.Select(item => new DashboardTrendMonth(
                item.ReferenceMonth,
                item.TotalIncome,
                item.TotalExpenses,
                item.Balance)).ToList(),
            BudgetAlerts = budget.Categories
                .Where(category => category.Planned > 0m && category.UsagePercentage >= 80m)
                .OrderByDescending(category => category.UsagePercentage)
                .Select(category => new DashboardBudgetAlert(
                    category.UsagePercentage >= 100m ? "CRITICAL" : "WARNING",
                    category.Category,
                    category.Name,
                    category.UsagePercentage,
                    category.Planned,
                    category.Spent,
                    category.Remaining))
                .ToList(),
            Goals = goals
                .OrderBy(goal => goal.Status == "COMPLETED")
                .ThenBy(goal => goal.TargetDate)
                .Take(4)
                .Select(goal => new DashboardFinancialGoal(
                    goal.Id,
                    goal.Name,
                    goal.TargetAmount,
                    goal.CurrentAmount,
                    goal.RemainingAmount,
                    goal.ProgressPercentage,
                    goal.TargetDate,
                    goal.Status,
                    goal.RequiredMonthlyContribution))
                .ToList(),
            CashFlowProjection = new DashboardCashFlowProjection(
                projection.ReferenceDate,
                projection.Months,
                projection.CurrentRecordedBalance,
                projection.TotalProjectedIncome,
                projection.TotalProjectedExpenses,
                projection.ProjectedCumulativeBalance,
                projection.Items.Select(item => new DashboardCashFlowProjectionMonth(
                    item.ReferenceMonth,
                    item.ProjectedIncome,
                    item.ProjectedExpenses,
                    item.ProjectedNet,
                    item.CumulativeBalance)).ToList())
        });
    }
}
