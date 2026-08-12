using System.Net;
using System.Text;
using FinanceControl.Bff.Ai;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Tests;

public sealed class OpenAiCompatibleAnalysisProviderTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesConfiguredEndpointAndKeepsDeterministicMetrics()
    {
        var handler = new CompletionHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://provider.example/openai/v1/")
        };
        var options = Options.Create(new AiProviderOptions
        {
            Provider = AiProviderOptions.OpenAiCompatibleProvider,
            BaseUrl = "https://provider.example/openai/v1/",
            ApiKey = "test-key",
            Model = "test-model",
            MaxOutputTokens = 500
        });
        var provider = new OpenAiCompatibleAnalysisProvider(
            httpClient,
            options,
            TimeProvider.System);
        var context = CreateContext();

        var response = await provider.AnalyzeAsync(context, CancellationToken.None);

        Assert.Equal("https://provider.example/openai/v1/chat/completions", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
        Assert.Contains("\"model\":\"test-model\"", handler.RequestBody);
        Assert.DoesNotContain("email", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("openai-compatible:test-model", response.Provider);
        Assert.Equal(900m, response.Metrics.TotalIncome);
        Assert.Equal(125m, response.Metrics.TotalOwed);
        Assert.Equal("WARNING", Assert.Single(response.DebtInsights).Severity);
    }

    private static AiAnalysisContext CreateContext() => new(
        "2026-08",
        900m,
        600m,
        300m,
        [new AiCategoryContext("FOOD", 400m)],
        125m,
        20m,
        2,
        1,
        1,
        0,
        [new AiDebtCategoryContext("FOOD", 125m, 20m, 2)],
        [new AiDebtGroupContext("Grupo 1", 125m, 20m, 2)],
        [new AiDebtDriverContext("FOOD", "Grupo 1", 125m, 0m, null, false)],
        3,
        2,
        [new AiBudgetCategoryContext("FOOD", 500m, 400m, 100m, 80m)],
        [new AiMonthlyTrendContext("2026-08", 900m, 600m, 300m)],
        [new AiGoalContext("Meta 1", 10_000m, 2_500m, 7_500m, 25m, new DateOnly(2027, 1, 1), "ACTIVE", 1_500m)],
        new AiCashFlowProjectionContext(
            1_800m,
            [new AiProjectionMonthContext("2026-08", 900m, 600m, 300m, 300m)]));

    private sealed class CompletionHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            const string response = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"overview\":\"Resumo externo\",\"financeInsights\":[],\"debtInsights\":[{\"severity\":\"warning\",\"title\":\"Atenção\",\"description\":\"Existe uma dívida vencida.\"}],\"recommendations\":[\"Revise seus vencimentos.\"]}"
                      }
                    }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
