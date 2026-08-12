using FinanceControl.Bff.Clients.Finance;

namespace FinanceControl.Bff.Tests;

internal abstract class FinanceServiceClientStub : IFinanceServiceClient
{
    public virtual Task DeleteAccountDataAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public virtual Task<FinanceSummaryResponse> GetMonthlySummaryAsync(
        string? month,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FinanceTrendResponse> GetTrendAsync(
        string? month,
        int months,
        CancellationToken cancellationToken) =>
        Task.FromResult(new FinanceTrendResponse(
            month ?? "2026-08",
            months,
            []));

    public virtual Task<IReadOnlyList<FinanceCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FinanceCategoryResponse> CreateCategoryAsync(
        FinanceCategoryRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FinanceCategoryResponse> UpdateCategoryAsync(
        long id,
        FinanceCategoryRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteCategoryAsync(long id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken) =>
        GetIncomesAsync(cancellationToken);

    public virtual Task<IncomeResponse> GetIncomeAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IncomeGoalAllocationResponse> GetIncomeGoalAllocationsAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IncomeResponse> CreateIncomeAsync(
        IncomeRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IncomeResponse> UpdateIncomeAsync(
        Guid id,
        IncomeRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteIncomeAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
        DateOnly? from,
        DateOnly? to,
        string? category,
        CancellationToken cancellationToken) =>
        GetExpensesAsync(cancellationToken);

    public virtual Task<ExpenseResponse> GetExpenseAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<ExpenseResponse> CreateExpenseAsync(
        ExpenseRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<ExpenseResponse> UpdateExpenseAsync(
        Guid id,
        ExpenseRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteExpenseAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<RecurringTransactionResponse>> GetRecurringTransactionsAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<RecurringTransactionResponse> CreateRecurringTransactionAsync(
        RecurringTransactionRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<RecurringTransactionResponse> UpdateRecurringTransactionAsync(
        Guid id,
        UpdateRecurringTransactionRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteRecurringTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<MonthlyBudgetResponse> GetMonthlyBudgetAsync(
        string? month,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MonthlyBudgetResponse(
            month ?? "2026-08",
            0m,
            0m,
            0m,
            []));

    public virtual Task<MonthlyBudgetResponse> SetMonthlyBudgetAsync(
        string month,
        string category,
        BudgetRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<MonthlyBudgetResponse> DeleteMonthlyBudgetAsync(
        string month,
        string category,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<FinancialGoalResponse>> GetFinancialGoalsAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FinancialGoalResponse>>([]);

    public virtual Task<FinancialGoalResponse> GetFinancialGoalAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FinancialGoalResponse> CreateFinancialGoalAsync(
        FinancialGoalRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FinancialGoalResponse> UpdateFinancialGoalAsync(
        Guid id,
        FinancialGoalRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteFinancialGoalAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<FinancialGoalContributionResponse>>
        GetFinancialGoalContributionsAsync(
            Guid goalId,
            CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FinancialGoalContributionResponse>>([]);

    public virtual Task<FinancialGoalContributionResponse> CreateFinancialGoalContributionAsync(
        Guid goalId,
        FinancialGoalContributionRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteFinancialGoalContributionAsync(
        Guid goalId,
        Guid contributionId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<CashFlowProjectionResponse> GetCashFlowProjectionAsync(
        int months,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CashFlowProjectionResponse(
            new DateOnly(2026, 8, 1),
            months,
            0m,
            0m,
            0m,
            0m,
            []));
}
