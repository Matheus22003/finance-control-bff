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
        var debtSummaryTask = debtServiceClient.GetSummaryAsync(cancellationToken);

        await Task.WhenAll(financeSummaryTask, financeTrendTask, budgetTask, debtSummaryTask);

        var financeSummary = await financeSummaryTask;
        var financeTrend = await financeTrendTask;
        var budget = await budgetTask;
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
                    category.UsagePercentage,
                    category.Planned,
                    category.Spent,
                    category.Remaining))
                .ToList()
        });
    }
}
