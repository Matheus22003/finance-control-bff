namespace FinanceControl.Bff.Contracts.Dashboard;

public record DashboardResponse
{
    public decimal Balance { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }
    public DebtsSummary DebtsSummary { get; init; } = default!;
}

public record DebtsSummary
{
    public decimal TotalOwed { get; init; }
    public decimal TotalToReceive { get; init; }
}