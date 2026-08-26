using System.Net.Http.Json;
using FinanceControl.Bff.Clients;

namespace FinanceControl.Bff.Clients.Finance;

public sealed class FinanceServiceClient(HttpClient httpClient) : IFinanceServiceClient
{
    private const string FinanceBasePath = "/api/v1/finance";
    private const string AccountDataPath = "/api/v1/internal/account-data";

    public Task DeleteAccountDataAsync(CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            AccountDataPath,
            cancellationToken);

    public Task<FinanceSummaryResponse> GetMonthlySummaryAsync(
        string? month,
        CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(month)
            ? $"{FinanceBasePath}/summary"
            : $"{FinanceBasePath}/summary?month={Uri.EscapeDataString(month)}";

        return SendForJsonAsync<FinanceSummaryResponse>(HttpMethod.Get, path, null, cancellationToken);
    }

    public Task<FinanceTrendResponse> GetTrendAsync(
        string? month,
        int months,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinanceTrendResponse>(
            HttpMethod.Get,
            BuildQuery(
                $"{FinanceBasePath}/trends",
                ("month", month),
                ("months", months.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            null,
            cancellationToken);

    public Task<FinanceReportResponse> GetReportAsync(
        string fromMonth,
        string toMonth,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinanceReportResponse>(
            HttpMethod.Get,
            BuildQuery(
                $"{FinanceBasePath}/reports/overview",
                ("from", fromMonth),
                ("to", toMonth)),
            null,
            cancellationToken);

    public Task<IReadOnlyList<FinanceCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FinanceCategoryResponse>>(
            HttpMethod.Get,
            $"{FinanceBasePath}/categories",
            null,
            cancellationToken);

    public Task<FinanceCategoryResponse> CreateCategoryAsync(
        FinanceCategoryRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinanceCategoryResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/categories",
            request,
            cancellationToken);

    public Task<FinanceCategoryResponse> UpdateCategoryAsync(
        long id,
        FinanceCategoryRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinanceCategoryResponse>(
            HttpMethod.Put,
            $"{FinanceBasePath}/categories/{id}",
            request,
            cancellationToken);

    public Task DeleteCategoryAsync(long id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/categories/{id}",
            cancellationToken);

    public Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(CancellationToken cancellationToken) =>
        GetIncomesAsync(null, null, cancellationToken);

    public Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<IncomeResponse>>(
            HttpMethod.Get,
            BuildQuery($"{FinanceBasePath}/incomes", ("from", from?.ToString("yyyy-MM-dd")), ("to", to?.ToString("yyyy-MM-dd"))),
            null,
            cancellationToken);

    public Task<IncomeResponse> GetIncomeAsync(Guid id, CancellationToken cancellationToken) =>
        SendForJsonAsync<IncomeResponse>(
            HttpMethod.Get,
            $"{FinanceBasePath}/incomes/{id}",
            null,
            cancellationToken);

    public Task<IncomeGoalAllocationResponse> GetIncomeGoalAllocationsAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IncomeGoalAllocationResponse>(
            HttpMethod.Get,
            $"{FinanceBasePath}/incomes/{id}/goal-allocations",
            null,
            cancellationToken);

    public Task<IncomeResponse> CreateIncomeAsync(
        IncomeRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IncomeResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/incomes",
            request,
            cancellationToken);

    public Task<IncomeResponse> UpdateIncomeAsync(
        Guid id,
        IncomeRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IncomeResponse>(
            HttpMethod.Put,
            $"{FinanceBasePath}/incomes/{id}",
            request,
            cancellationToken);

    public Task DeleteIncomeAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/incomes/{id}",
            cancellationToken);

    public Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(CancellationToken cancellationToken) =>
        GetExpensesAsync(null, null, null, cancellationToken);

    public Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
        DateOnly? from,
        DateOnly? to,
        string? category,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<ExpenseResponse>>(
            HttpMethod.Get,
            BuildQuery(
                $"{FinanceBasePath}/expenses",
                ("from", from?.ToString("yyyy-MM-dd")),
                ("to", to?.ToString("yyyy-MM-dd")),
                ("category", category)),
            null,
            cancellationToken);

    public Task<ExpenseResponse> GetExpenseAsync(Guid id, CancellationToken cancellationToken) =>
        SendForJsonAsync<ExpenseResponse>(
            HttpMethod.Get,
            $"{FinanceBasePath}/expenses/{id}",
            null,
            cancellationToken);

    public Task<ExpenseResponse> CreateExpenseAsync(
        ExpenseRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<ExpenseResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/expenses",
            request,
            cancellationToken);

    public Task<ExpenseResponse> UpdateExpenseAsync(
        Guid id,
        ExpenseRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<ExpenseResponse>(
            HttpMethod.Put,
            $"{FinanceBasePath}/expenses/{id}",
            request,
            cancellationToken);

    public Task DeleteExpenseAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/expenses/{id}",
            cancellationToken);

    public Task<IReadOnlyList<RecurringTransactionResponse>> GetRecurringTransactionsAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<RecurringTransactionResponse>>(
            HttpMethod.Get,
            $"{FinanceBasePath}/recurring-transactions",
            null,
            cancellationToken);

    public Task<RecurringTransactionResponse> CreateRecurringTransactionAsync(
        RecurringTransactionRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<RecurringTransactionResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/recurring-transactions",
            request,
            cancellationToken);

    public Task<RecurringTransactionResponse> UpdateRecurringTransactionAsync(
        Guid id,
        UpdateRecurringTransactionRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<RecurringTransactionResponse>(
            HttpMethod.Put,
            $"{FinanceBasePath}/recurring-transactions/{id}",
            request,
            cancellationToken);

    public Task DeleteRecurringTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/recurring-transactions/{id}",
            cancellationToken);

    public Task<MonthlyBudgetResponse> GetMonthlyBudgetAsync(
        string? month,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<MonthlyBudgetResponse>(
            HttpMethod.Get,
            BuildQuery($"{FinanceBasePath}/budgets", ("month", month)),
            null,
            cancellationToken);

    public Task<MonthlyBudgetResponse> SetMonthlyBudgetAsync(
        string month,
        string category,
        BudgetRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<MonthlyBudgetResponse>(
            HttpMethod.Put,
            BuildQuery(
                $"{FinanceBasePath}/budgets/{Uri.EscapeDataString(category)}",
                ("month", month)),
            request,
            cancellationToken);

    public Task<MonthlyBudgetResponse> DeleteMonthlyBudgetAsync(
        string month,
        string category,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<MonthlyBudgetResponse>(
            HttpMethod.Delete,
            BuildQuery(
                $"{FinanceBasePath}/budgets/{Uri.EscapeDataString(category)}",
                ("month", month)),
            null,
            cancellationToken);

    public Task<IReadOnlyList<FinancialGoalResponse>> GetFinancialGoalsAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FinancialGoalResponse>>(
            HttpMethod.Get,
            $"{FinanceBasePath}/goals",
            null,
            cancellationToken);

    public Task<FinancialGoalResponse> GetFinancialGoalAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinancialGoalResponse>(
            HttpMethod.Get,
            $"{FinanceBasePath}/goals/{id}",
            null,
            cancellationToken);

    public Task<FinancialGoalResponse> CreateFinancialGoalAsync(
        FinancialGoalRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinancialGoalResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/goals",
            request,
            cancellationToken);

    public Task<FinancialGoalResponse> UpdateFinancialGoalAsync(
        Guid id,
        FinancialGoalRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinancialGoalResponse>(
            HttpMethod.Put,
            $"{FinanceBasePath}/goals/{id}",
            request,
            cancellationToken);

    public Task DeleteFinancialGoalAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/goals/{id}",
            cancellationToken);

    public Task<IReadOnlyList<FinancialGoalContributionResponse>>
        GetFinancialGoalContributionsAsync(
            Guid goalId,
            CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FinancialGoalContributionResponse>>(
            HttpMethod.Get,
            $"{FinanceBasePath}/goals/{goalId}/contributions",
            null,
            cancellationToken);

    public Task<FinancialGoalContributionResponse> CreateFinancialGoalContributionAsync(
        Guid goalId,
        FinancialGoalContributionRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FinancialGoalContributionResponse>(
            HttpMethod.Post,
            $"{FinanceBasePath}/goals/{goalId}/contributions",
            request,
            cancellationToken);

    public Task DeleteFinancialGoalContributionAsync(
        Guid goalId,
        Guid contributionId,
        CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FinanceBasePath}/goals/{goalId}/contributions/{contributionId}",
            cancellationToken);

    public Task<CashFlowProjectionResponse> GetCashFlowProjectionAsync(
        int months,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<CashFlowProjectionResponse>(
            HttpMethod.Get,
            BuildQuery(
                $"{FinanceBasePath}/projections/cash-flow",
                ("months", months.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            null,
            cancellationToken);

    private static string BuildQuery(string path, params (string Name, string? Value)[] parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value!)}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    private async Task<TResponse> SendForJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        object? requestBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (requestBody is not null)
        {
            request.Content = JsonContent.Create(requestBody);
        }

        using var response = await SendAsync(request, cancellationToken);
        return await UpstreamResponseReader.ReadRequiredJsonAsync<TResponse>(
            response,
            "Finance Service",
            (message, exception) => new FinanceServiceException(
                FinanceServiceFailure.InvalidResponse,
                message,
                exception),
            cancellationToken);
    }

    private async Task SendWithoutResponseBodyAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        using var response = await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = (int)response.StatusCode;
            var problem = await UpstreamResponseReader.ReadProblemDetailsAsync(response, cancellationToken);
            response.Dispose();

            throw new FinanceServiceException(
                statusCode is >= 400 and < 500
                    ? FinanceServiceFailure.Rejected
                    : FinanceServiceFailure.InvalidResponse,
                $"Finance Service returned HTTP {statusCode}.",
                upstreamStatusCode: statusCode,
                upstreamProblem: problem);
        }
        catch (FinanceServiceException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FinanceServiceException(
                FinanceServiceFailure.Timeout,
                "Finance Service request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FinanceServiceException(
                FinanceServiceFailure.Unavailable,
                "Finance Service could not be reached.",
                exception);
        }
    }
}
