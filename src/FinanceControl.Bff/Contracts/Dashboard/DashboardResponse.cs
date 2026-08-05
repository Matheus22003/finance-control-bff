namespace FinanceControl.Bff.Contracts.Dashboard;

public record DashboardResponse
{
    public decimal Balance { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }
    public DebtsSummary DebtsSummary { get; init; } = default!;
    public DashboardBudgetSummary Budget { get; init; } = default!;
    public IReadOnlyList<DashboardTrendMonth> MonthlyTrend { get; init; } = [];
    public IReadOnlyList<DashboardBudgetAlert> BudgetAlerts { get; init; } = [];
}

public record DebtsSummary
{
    public decimal TotalOwed { get; init; }
    public decimal TotalToReceive { get; init; }
    public int OpenDebtsCount { get; init; }
}

public sealed record DashboardBudgetSummary(
    string ReferenceMonth,
    decimal TotalPlanned,
    decimal TotalSpent,
    decimal TotalRemaining,
    IReadOnlyList<DashboardBudgetCategory> Categories);

public sealed record DashboardBudgetCategory(
    string Category,
    decimal Planned,
    decimal Spent,
    decimal Remaining,
    decimal UsagePercentage);

public sealed record DashboardTrendMonth(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance);

public sealed record DashboardBudgetAlert(
    string Severity,
    string Category,
    decimal UsagePercentage,
    decimal Planned,
    decimal Spent,
    decimal Remaining);
