namespace FinanceControl.Bff.Clients.Finance;

public sealed record FinanceSummaryResponse(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance);
