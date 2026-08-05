namespace FinanceControl.Bff.Clients.Debt;

public sealed record DebtSummaryResponse(
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);
