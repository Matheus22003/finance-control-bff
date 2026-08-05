namespace FinanceControl.Bff.Contracts.Ai;

public sealed record AiAnalysisRequest(string? Month);

public sealed record AiAnalysisResponse(
    DateTimeOffset GeneratedAt,
    string Provider,
    string ReferenceMonth,
    string Overview,
    AiAnalysisMetrics Metrics,
    IReadOnlyList<AiInsightResponse> FinanceInsights,
    IReadOnlyList<AiInsightResponse> DebtInsights,
    IReadOnlyList<string> Recommendations);

public sealed record AiAnalysisMetrics(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int OverdueDebtsCount,
    int DueSoonDebtsCount,
    int OriginalTransferCount,
    int SimplifiedTransferCount);

public sealed record AiInsightResponse(
    string Severity,
    string Title,
    string Description);

public sealed record AiQuestionRequest(string Question);

public sealed record AiQuestionResponse(
    DateTimeOffset GeneratedAt,
    string Provider,
    string Answer,
    IReadOnlyList<string> SuggestedQuestions);
