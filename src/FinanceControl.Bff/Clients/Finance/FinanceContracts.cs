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
    public decimal GoalAllocatedAmount { get; init; }
    public decimal GoalAvailableAmount { get; init; }
}

public sealed record IncomeGoalAllocationItemResponse(
    Guid ContributionId,
    Guid FinancialGoalId,
    string FinancialGoalName,
    decimal Amount,
    DateOnly ContributionDate,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record IncomeGoalAllocationResponse(
    Guid IncomeId,
    string IncomeDescription,
    decimal IncomeAmount,
    DateOnly TransactionDate,
    decimal GoalAllocatedAmount,
    decimal GoalAvailableAmount,
    IReadOnlyList<IncomeGoalAllocationItemResponse> Allocations);

public sealed record FinanceCategoryRequest(string Name);

public sealed record FinanceCategoryResponse(
    long Id,
    string Code,
    string Name,
    bool DefaultCategory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

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
    string Name,
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

public sealed record FinancialGoalRequest(
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly TargetDate);

public sealed record FinancialGoalResponse(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal RemainingAmount,
    decimal ProgressPercentage,
    DateOnly TargetDate,
    string Status,
    decimal RequiredMonthlyContribution,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FinancialGoalContributionRequest(
    decimal Amount,
    DateOnly ContributionDate,
    string? Note,
    Guid? SourceIncomeId = null);

public sealed record FinancialGoalContributionSourceResponse(
    Guid? IncomeId,
    string Description,
    decimal IncomeAmount,
    DateOnly TransactionDate);

public sealed record FinancialGoalContributionResponse(
    Guid Id,
    Guid FinancialGoalId,
    decimal Amount,
    DateOnly ContributionDate,
    string? Note,
    string Type,
    DateTimeOffset CreatedAt,
    FinancialGoalContributionSourceResponse? Source = null);

public sealed record CashFlowProjectionMonthResponse(
    string ReferenceMonth,
    decimal ProjectedIncome,
    decimal ProjectedExpenses,
    decimal ProjectedNet,
    decimal CumulativeBalance);

public sealed record CashFlowProjectionResponse(
    DateOnly ReferenceDate,
    int Months,
    decimal CurrentRecordedBalance,
    decimal TotalProjectedIncome,
    decimal TotalProjectedExpenses,
    decimal ProjectedCumulativeBalance,
    IReadOnlyList<CashFlowProjectionMonthResponse> Items);
