using System.Text.Json;
using FinanceControl.Bff.Ai;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Tests;

public sealed class AiQuestionServiceTests
{
    [Theory]
    [InlineData("Quem ainda me deve pela comida?", "Ana ainda deve R$ 50,00")]
    [InlineData("De onde acumulei minhas dívidas?", "não possui valores a pagar")]
    [InlineData("Quanto gastei com alimentação?", "R$ 75,00 com alimentação")]
    public async Task AskAsync_AnswersExactFinancialFactsWithoutCallingTheModel(
        string question,
        string expectedAnswer)
    {
        var provider = new CapturingProvider();
        var service = new AiQuestionService(
            new FinanceClient(),
            new DebtClient(),
            provider,
            TimeProvider.System);

        var response = await service.AskAsync(question, CancellationToken.None);

        Assert.Equal("deterministic", response.Provider);
        Assert.Contains(expectedAnswer, response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Null(provider.Context);
    }

    [Fact]
    public async Task AskAsync_ReplacesPrivateDataWithAliasesAndRestoresOnlyTheAnswer()
    {
        var provider = new CapturingProvider();
        var service = new AiQuestionService(
            new FinanceClient(),
            new DebtClient(),
            provider,
            TimeProvider.System);

        var response = await service.AskAsync(
            "Quanto a Ana deve no Jantar secreto? Envie para ana@example.com.",
            CancellationToken.None);

        Assert.NotNull(provider.Context);
        var serializedContext = JsonSerializer.Serialize(provider.Context);
        Assert.DoesNotContain("Ana", serializedContext, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jantar secreto", serializedContext, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Amigos próximos", serializedContext, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@example.com", serializedContext, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pessoa 1", provider.Context.Question);
        Assert.Contains("Dívida 1", provider.Context.Question);
        Assert.Contains("Ana", response.Answer);
        Assert.Contains("Jantar secreto", response.Answer);
        Assert.Contains("Amigos próximos", response.Answer);
    }

    private sealed class CapturingProvider : IAiAnalysisProvider
    {
        public string Name => "capture";
        public AiQuestionContext? Context { get; private set; }

        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisContext context,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiQuestionResponse> AskAsync(
            AiQuestionContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            return Task.FromResult(new AiQuestionResponse(
                DateTimeOffset.UtcNow,
                Name,
                "Pessoa 1 ainda deve R$ 50,00 na Dívida 1 do Grupo 1.",
                ["Quanto a Pessoa 1 já pagou?"]));
        }
    }

    private sealed class FinanceClient : FinanceServiceClientStub
    {
        public override Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomeResponse>>([]);

        public override Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExpenseResponse>>([
                new ExpenseResponse(
                    Guid.NewGuid(),
                    "Mercado pessoal",
                    75m,
                    new DateOnly(2026, 8, 1),
                    "FOOD",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private sealed class DebtClient : DebtServiceClientStub
    {
        private static readonly Guid GroupId = Guid.Parse("00000000-0000-0000-0000-000000000011");

        public override Task<IReadOnlyList<DebtResponse>> GetDebtsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DebtResponse>>([
                new DebtResponse(
                    Guid.NewGuid(),
                    "Jantar secreto",
                    100m,
                    new PersonReferenceResponse(Guid.NewGuid(), "Conta atual", true),
                    GroupId,
                    "FOOD",
                    "OPEN",
                    null,
                    true,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [
                        new DebtShareResponse(
                            Guid.NewGuid(),
                            new PersonReferenceResponse(Guid.NewGuid(), "Ana", false),
                            50m,
                            0m,
                            50m,
                            false)
                    ])
            ]);

        public override Task<IReadOnlyList<GroupResponse>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupResponse>>([
                new GroupResponse(
                    GroupId,
                    "Amigos próximos",
                    null,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [])
            ]);
    }
}
