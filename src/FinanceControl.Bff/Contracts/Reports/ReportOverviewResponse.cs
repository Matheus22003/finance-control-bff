namespace FinanceControl.Bff.Contracts.Reports;

public sealed record ReportOverviewResponse(
    string FromMonth,
    string ToMonth,
    int MonthCount,
    DateTimeOffset GeneratedAt,
    ReportFinanceSection Finance,
    ReportDebtSection Debts,
    ReportHighlights Highlights);

public sealed record ReportFinanceSection(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance,
    decimal SavingsRatePercentage,
    long IncomeCount,
    long ExpenseCount,
    IReadOnlyList<ReportFinanceMonth> Months,
    IReadOnlyList<ReportFinanceCategory> ExpenseCategories,
    IReadOnlyList<ReportExpenseItem> TopExpenses);

public sealed record ReportFinanceMonth(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance);

public sealed record ReportFinanceCategory(
    string Category,
    string Name,
    decimal Amount,
    decimal Percentage);

public sealed record ReportExpenseItem(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string Category,
    string CategoryName);

public sealed record ReportDebtSection(
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int PaidDebtsCount,
    IReadOnlyList<ReportDebtMonth> Months,
    IReadOnlyList<ReportDebtCategory> Categories,
    IReadOnlyList<ReportDebtItem> TopDebts);

public sealed record ReportDebtMonth(
    string ReferenceMonth,
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int DebtCount);

public sealed record ReportDebtCategory(
    string Category,
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int DebtCount);

public sealed record ReportDebtItem(
    Guid Id,
    string Description,
    string Category,
    decimal TotalAmount,
    decimal TotalOwed,
    decimal TotalToReceive,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt);

public sealed record ReportHighlights(
    decimal AverageMonthlyIncome,
    decimal AverageMonthlyExpenses,
    string? BestBalanceMonth,
    string? HighestExpenseCategory);
