namespace FinanceControl.Bff.Clients.Finance;

public sealed record IncomeRequest(
    string Description,
    decimal Amount,
    DateOnly TransactionDate);

public sealed record IncomeResponse(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Guid? RecurringTransactionId { get; init; }
}

public sealed record ExpenseRequest(
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string Category);

public sealed record ExpenseResponse(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Guid? RecurringTransactionId { get; init; }
}

public sealed record RecurringTransactionRequest(
    string Kind,
    string Description,
    decimal Amount,
    string? Category,
    string Frequency,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateRecurringTransactionRequest(
    string Description,
    decimal Amount,
    string? Category,
    DateOnly? EndDate,
    bool Active);

public sealed record RecurringTransactionResponse(
    Guid Id,
    string Kind,
    string Description,
    decimal Amount,
    string? Category,
    string Frequency,
    DateOnly StartDate,
    DateOnly NextOccurrenceDate,
    DateOnly? EndDate,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BudgetRequest(decimal Amount);

public sealed record BudgetCategoryResponse(
    string Category,
    decimal Planned,
    decimal Spent,
    decimal Remaining,
    decimal UsagePercentage);

public sealed record MonthlyBudgetResponse(
    string ReferenceMonth,
    decimal TotalPlanned,
    decimal TotalSpent,
    decimal TotalRemaining,
    IReadOnlyList<BudgetCategoryResponse> Categories);

public sealed record FinanceTrendMonthResponse(
    string ReferenceMonth,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance);

public sealed record FinanceTrendResponse(
    string ReferenceMonth,
    int Months,
    IReadOnlyList<FinanceTrendMonthResponse> Items);
