using System.Globalization;
using FinanceControl.Bff.Ai;
using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Endpoints;

public static class AiEndpoints
{
    public static RouteGroupBuilder MapAiEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/ai/analyze", Analyze)
            .WithName("AnalyzeFinancialLife")
            .WithTags("AI")
            .Produces<AiAnalysisResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireAuthorization()
            .RequireRateLimiting("ai-analysis");

        group.MapPost("/ai/ask", Ask)
            .WithName("AskAboutFinancialLife")
            .WithTags("AI")
            .Produces<AiQuestionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireAuthorization()
            .RequireRateLimiting("ai-question");

        return group;
    }

    private static async Task<IResult> Ask(
        AiQuestionRequest request,
        AiQuestionService service,
        CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question) || question.Length is < 3 or > 500)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["question"] = ["Question must contain between 3 and 500 characters."]
            });
        }

        return Results.Ok(await service.AskAsync(question, cancellationToken));
    }

    private static async Task<IResult> Analyze(
        AiAnalysisRequest request,
        AiAnalysisService service,
        CancellationToken cancellationToken)
    {
        if (!IsValidMonth(request.Month))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["month"] = ["Month must use the yyyy-MM format."]
            });
        }

        return Results.Ok(await service.AnalyzeAsync(request.Month, cancellationToken));
    }

    private static bool IsValidMonth(string? month) =>
        string.IsNullOrWhiteSpace(month) ||
        DateOnly.TryParseExact(
            $"{month}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
}
