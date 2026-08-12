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
    public async Task GetIncomeAsync_DeserializesGoalAllocationAmounts()
    {
        var id = Guid.Parse("907888b3-81cc-44aa-805f-a6b114acfc30");
        var json = $$"""
                     {
                       "id": "{{id}}",
                       "description": "Freelance",
                       "amount": 1500.00,
                       "transactionDate": "2026-08-11",
                       "createdAt": "2026-08-11T12:00:00Z",
                       "updatedAt": "2026-08-11T12:00:00Z",
                       "goalAllocatedAmount": 500.00,
                       "goalAvailableAmount": 1000.00
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var income = await client.GetIncomeAsync(id, CancellationToken.None);

        Assert.Equal(500m, income.GoalAllocatedAmount);
        Assert.Equal(1_000m, income.GoalAvailableAmount);
    }

    [Fact]
    public async Task GetIncomeGoalAllocationsAsync_DeserializesGoalDetails()
    {
        var incomeId = Guid.Parse("907888b3-81cc-44aa-805f-a6b114acfc30");
        var contributionId = Guid.Parse("6fd7c1ca-46d9-4db0-879a-7d696b26b338");
        var goalId = Guid.Parse("ee761e59-5510-4fd4-a25b-d086375f144f");
        var json = $$"""
                     {
                       "incomeId": "{{incomeId}}",
                       "incomeDescription": "Freelance",
                       "incomeAmount": 1500.00,
                       "transactionDate": "2026-08-11",
                       "goalAllocatedAmount": 500.00,
                       "goalAvailableAmount": 1000.00,
                       "allocations": [{
                         "contributionId": "{{contributionId}}",
                         "financialGoalId": "{{goalId}}",
                         "financialGoalName": "Reserva",
                         "amount": 500.00,
                         "contributionDate": "2026-08-11",
                         "note": "Aporte mensal",
                         "createdAt": "2026-08-11T12:00:00Z"
                       }]
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var details = await client.GetIncomeGoalAllocationsAsync(incomeId, CancellationToken.None);

        Assert.Equal(500m, details.GoalAllocatedAmount);
        Assert.Equal("Reserva", Assert.Single(details.Allocations).FinancialGoalName);
        Assert.Equal(
            $"/api/v1/finance/incomes/{incomeId}/goal-allocations",
            handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateCategoryAsync_ForwardsNameAndDeserializesCategory()
    {
        const string json = """
                            {
                              "id": 7,
                              "code": "CUSTOM_A1",
                              "name": "Academia",
                              "defaultCategory": false,
                              "createdAt": "2026-08-11T12:00:00Z",
                              "updatedAt": "2026-08-11T12:00:00Z"
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var category = await client.CreateCategoryAsync(
            new FinanceCategoryRequest("Academia"),
            CancellationToken.None);

        Assert.Equal(7, category.Id);
        Assert.Equal("CUSTOM_A1", category.Code);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/v1/finance/categories", handler.LastRequestUri?.AbsolutePath);
        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal("Academia", requestBody.RootElement.GetProperty("name").GetString());
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
    public async Task CreateFinancialGoalAsync_ForwardsContractAndDeserializesProgress()
    {
        var id = Guid.Parse("2c930785-fb19-4f62-a733-87a0b2d123bf");
        var json = $$"""
                     {
                       "id": "{{id}}",
                       "name": "Reserva",
                       "targetAmount": 10000.00,
                       "currentAmount": 2500.00,
                       "remainingAmount": 7500.00,
                       "progressPercentage": 25.00,
                       "targetDate": "2027-02-10",
                       "status": "ACTIVE",
                       "requiredMonthlyContribution": 1250.00,
                       "createdAt": "2026-08-10T12:00:00Z",
                       "updatedAt": "2026-08-10T12:00:00Z"
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var goal = await client.CreateFinancialGoalAsync(
            new FinancialGoalRequest("Reserva", 10_000m, 2_500m, new DateOnly(2027, 2, 10)),
            CancellationToken.None);

        Assert.Equal(id, goal.Id);
        Assert.Equal(25m, goal.ProgressPercentage);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/v1/finance/goals", handler.LastRequestUri?.AbsolutePath);
        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal("Reserva", requestBody.RootElement.GetProperty("name").GetString());
        Assert.Equal("2027-02-10", requestBody.RootElement.GetProperty("targetDate").GetString());
    }

    [Fact]
    public async Task CreateFinancialGoalContributionAsync_ForwardsLedgerContract()
    {
        var goalId = Guid.Parse("2c930785-fb19-4f62-a733-87a0b2d123bf");
        var contributionId = Guid.Parse("e44eb233-1e24-4a39-8149-0dbc6a355beb");
        var incomeId = Guid.Parse("61f1d6b7-e512-464a-b829-1ac481be0345");
        var json = $$"""
                     {
                       "id": "{{contributionId}}",
                       "financialGoalId": "{{goalId}}",
                       "amount": 500.00,
                       "contributionDate": "2026-08-11",
                       "note": "Economia do mês",
                       "type": "CONTRIBUTION",
                       "createdAt": "2026-08-11T12:00:00Z",
                       "source": {
                         "incomeId": "{{incomeId}}",
                         "description": "Monthly salary",
                         "incomeAmount": 8000.00,
                         "transactionDate": "2026-08-05"
                       }
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var contribution = await client.CreateFinancialGoalContributionAsync(
            goalId,
            new FinancialGoalContributionRequest(
                500m,
                new DateOnly(2026, 8, 11),
                "Economia do mês",
                incomeId),
            CancellationToken.None);

        Assert.Equal(contributionId, contribution.Id);
        Assert.Equal("CONTRIBUTION", contribution.Type);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(
            $"/api/v1/finance/goals/{goalId}/contributions",
            handler.LastRequestUri?.AbsolutePath);
        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal(500m, requestBody.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(
            "2026-08-11",
            requestBody.RootElement.GetProperty("contributionDate").GetString());
        Assert.Equal(
            incomeId,
            requestBody.RootElement.GetProperty("sourceIncomeId").GetGuid());
        Assert.Equal(incomeId, contribution.Source?.IncomeId);
        Assert.Equal("Monthly salary", contribution.Source?.Description);
    }

    [Fact]
    public async Task GetCashFlowProjectionAsync_ForwardsMonthsAndDeserializesItems()
    {
        const string json = """
                            {
                              "referenceDate": "2026-08-10",
                              "months": 6,
                              "currentRecordedBalance": 1000.00,
                              "totalProjectedIncome": 6000.00,
                              "totalProjectedExpenses": 3000.00,
                              "projectedCumulativeBalance": 3000.00,
                              "items": [
                                {
                                  "referenceMonth": "2026-08",
                                  "projectedIncome": 1000.00,
                                  "projectedExpenses": 500.00,
                                  "projectedNet": 500.00,
                                  "cumulativeBalance": 500.00
                                }
                              ]
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var projection = await client.GetCashFlowProjectionAsync(6, CancellationToken.None);

        Assert.Equal(3_000m, projection.ProjectedCumulativeBalance);
        Assert.Equal(500m, Assert.Single(projection.Items).ProjectedNet);
        Assert.Equal("?months=6", handler.LastRequestUri?.Query);
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
