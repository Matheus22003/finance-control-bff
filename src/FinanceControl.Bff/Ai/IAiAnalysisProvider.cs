using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Ai;

public interface IAiAnalysisProvider
{
    string Name { get; }

    Task<AiAnalysisResponse> AnalyzeAsync(
        AiAnalysisContext context,
        CancellationToken cancellationToken);

    Task<AiQuestionResponse> AskAsync(
        AiQuestionContext context,
        CancellationToken cancellationToken);
}
