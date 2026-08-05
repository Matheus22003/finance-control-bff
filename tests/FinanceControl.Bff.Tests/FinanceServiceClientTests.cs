using System.Net;
using System.Text;
using System.Text.Json;
using FinanceControl.Bff.Clients.Finance;

namespace FinanceControl.Bff.Tests;

public sealed class FinanceServiceClientTests
{
    [Fact]
    public async Task GetMonthlySummaryAsync_DeserializesFinanceServiceContract()
    {
        const string json = """
                            {
                              "referenceMonth": "2026-07",
                              "totalIncome": 5000.00,
                              "totalExpenses": 3749.25,
                              "balance": 1250.75,
                              "expensesByCategory": { "FOOD": 650.00 }
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var summary = await client.GetMonthlySummaryAsync(null, CancellationToken.None);

        Assert.Equal("2026-07", summary.ReferenceMonth);
        Assert.Equal(5_000.00m, summary.TotalIncome);
        Assert.Equal(3_749.25m, summary.TotalExpenses);
        Assert.Equal(1_250.75m, summary.Balance);
        Assert.Equal("/api/v1/finance/summary", handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_ForwardsMonthQueryString()
    {
        const string json = """
                            {
                              "referenceMonth": "2026-06",
                              "totalIncome": 0,
                              "totalExpenses": 0,
                              "balance": 0,
                              "expensesByCategory": {}
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        await client.GetMonthlySummaryAsync("2026-06", CancellationToken.None);

        Assert.Equal("?month=2026-06", handler.LastRequestUri?.Query);
    }

    [Fact]
    public async Task CreateExpenseAsync_ForwardsContractAndDeserializesResponse()
    {
        var id = Guid.Parse("e54edb88-3dc2-4534-b95b-f9c845683252");
        var json = $$"""
                     {
                       "id": "{{id}}",
                       "description": "Mercado",
                       "amount": 175.90,
                       "transactionDate": "2026-07-31",
                       "category": "FOOD",
                       "createdAt": "2026-07-31T12:00:00Z",
                       "updatedAt": "2026-07-31T12:00:00Z"
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var expense = await client.CreateExpenseAsync(
            new ExpenseRequest("Mercado", 175.90m, new DateOnly(2026, 7, 31), "FOOD"),
            CancellationToken.None);

        Assert.Equal(id, expense.Id);
        Assert.Equal("FOOD", expense.Category);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/v1/finance/expenses", handler.LastRequestUri?.AbsolutePath);

        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal("Mercado", requestBody.RootElement.GetProperty("description").GetString());
        Assert.Equal("FOOD", requestBody.RootElement.GetProperty("category").GetString());
        Assert.Equal("2026-07-31", requestBody.RootElement.GetProperty("transactionDate").GetString());
    }

    [Fact]
    public async Task GetIncomeAsync_MapsUpstreamProblemDetails()
    {
        const string json = """
                            {
                              "title": "Resource not found",
                              "status": 404,
                              "detail": "Income does not exist.",
                              "instance": "/api/v1/finance/incomes/00000000-0000-0000-0000-000000000000"
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<FinanceServiceException>(() =>
            client.GetIncomeAsync(Guid.Empty, CancellationToken.None));

        Assert.Equal(FinanceServiceFailure.Rejected, exception.Failure);
        Assert.Equal(404, exception.UpstreamStatusCode);
        Assert.Equal("Resource not found", exception.UpstreamProblem?.Title);
        Assert.Equal("Income does not exist.", exception.UpstreamProblem?.Detail);
    }

    [Fact]
    public async Task GetExpensesAsync_ForwardsDateAndCategoryFilters()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        await client.GetExpensesAsync(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            "FOOD",
            CancellationToken.None);

        Assert.Equal("?from=2026-07-01&to=2026-07-31&category=FOOD", handler.LastRequestUri?.Query);
    }

    [Fact]
    public async Task SetMonthlyBudgetAsync_ForwardsMonthCategoryAndAmount()
    {
        const string json = """
                            {
                              "referenceMonth": "2026-08",
                              "totalPlanned": 500.00,
                              "totalSpent": 0,
                              "totalRemaining": 500.00,
                              "categories": []
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var budget = await client.SetMonthlyBudgetAsync(
            "2026-08",
            "FOOD",
            new BudgetRequest(500m),
            CancellationToken.None);

        Assert.Equal(500m, budget.TotalPlanned);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal("/api/v1/finance/budgets/FOOD", handler.LastRequestUri?.AbsolutePath);
        Assert.Equal("?month=2026-08", handler.LastRequestUri?.Query);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_MapsUpstreamHttpError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<FinanceServiceException>(
            () => client.GetMonthlySummaryAsync(null, CancellationToken.None));

        Assert.Equal(FinanceServiceFailure.InvalidResponse, exception.Failure);
        Assert.Equal(500, exception.UpstreamStatusCode);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_MapsConnectionFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<FinanceServiceException>(
            () => client.GetMonthlySummaryAsync(null, CancellationToken.None));

        Assert.Equal(FinanceServiceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task DeleteAccountDataAsync_CallsInternalAccountEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.DeleteAccountDataAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal("/api/v1/internal/account-data", handler.LastRequestUri?.AbsolutePath);
    }

    private static FinanceServiceClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://finance-service.test"),
            Timeout = TimeSpan.FromSeconds(2)
        };

        return new FinanceServiceClient(httpClient);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
