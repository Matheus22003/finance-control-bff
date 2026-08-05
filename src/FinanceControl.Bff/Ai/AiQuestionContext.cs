namespace FinanceControl.Bff.Ai;

public sealed record AiQuestionContext(
    string Question,
    DateTimeOffset GeneratedAt,
    bool ContextWasTruncated,
    IReadOnlyList<AiTransactionQuestionContext> Transactions,
    IReadOnlyList<AiDebtQuestionContext> Debts,
    IReadOnlyList<AiReceivableQuestionContext> Receivables,
    IReadOnlyList<AiPayableCategoryQuestionContext> PayablesByCategory,
    IReadOnlyList<AiBudgetCategoryContext> BudgetCategories,
    IReadOnlyList<AiMonthlyTrendContext> MonthlyTrend);

public sealed record AiTransactionQuestionContext(
    string Alias,
    string Kind,
    string? Category,
    string? CategoryLabel,
    decimal Amount,
    DateOnly TransactionDate);

public sealed record AiDebtQuestionContext(
    string Alias,
    string Category,
    string CategoryLabel,
    string GroupAlias,
    decimal TotalAmount,
    decimal CurrentUserOwes,
    decimal OwedToCurrentUser,
    string PositionDirection,
    string PaidByAlias,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AiDebtParticipantContext> Participants);

public sealed record AiDebtParticipantContext(
    string PersonAlias,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsPayer,
    bool IsCurrentUser);

public sealed record AiReceivableQuestionContext(
    string PersonAlias,
    string Category,
    string CategoryLabel,
    decimal Amount);

public sealed record AiPayableCategoryQuestionContext(
    string Category,
    string CategoryLabel,
    decimal Amount);
