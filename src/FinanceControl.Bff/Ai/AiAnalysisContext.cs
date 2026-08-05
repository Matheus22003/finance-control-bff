namespace FinanceControl.Bff.Ai;

public sealed record AiAnalysisContext(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance,
    IReadOnlyList<AiCategoryContext> ExpenseCategories,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int PaidDebtsCount,
    int OverdueDebtsCount,
    int DueSoonDebtsCount,
    IReadOnlyList<AiDebtCategoryContext> DebtCategories,
    IReadOnlyList<AiDebtGroupContext> DebtGroups,
    IReadOnlyList<AiDebtDriverContext> TopDebtDrivers,
    int OriginalTransferCount,
    int SimplifiedTransferCount,
    IReadOnlyList<AiBudgetCategoryContext> BudgetCategories,
    IReadOnlyList<AiMonthlyTrendContext> MonthlyTrend);

public sealed record AiCategoryContext(string Category, decimal Amount);

public sealed record AiDebtCategoryContext(
    string Category,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record AiDebtGroupContext(
    string Alias,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record AiDebtDriverContext(
    string Category,
    string GroupAlias,
    decimal TotalOwed,
    decimal TotalToReceive,
    DateOnly? DueDate,
    bool IsOverdue);

public sealed record AiBudgetCategoryContext(
    string Category,
    decimal Planned,
    decimal Spent,
    decimal Remaining,
    decimal UsagePercentage);

public sealed record AiMonthlyTrendContext(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance);
