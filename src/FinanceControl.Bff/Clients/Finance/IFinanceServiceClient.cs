namespace FinanceControl.Bff.Clients.Finance;

public interface IFinanceServiceClient
{
    Task DeleteAccountDataAsync(CancellationToken cancellationToken);

    Task<FinanceSummaryResponse> GetMonthlySummaryAsync(
        string? month,
        CancellationToken cancellationToken);

    Task<FinanceTrendResponse> GetTrendAsync(
        string? month,
        int months,
        CancellationToken cancellationToken);

    Task<FinanceReportResponse> GetReportAsync(
        string fromMonth,
        string toMonth,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinanceCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken);

    Task<FinanceCategoryResponse> CreateCategoryAsync(
        FinanceCategoryRequest request,
        CancellationToken cancellationToken);

    Task<FinanceCategoryResponse> UpdateCategoryAsync(
        long id,
        FinanceCategoryRequest request,
        CancellationToken cancellationToken);

    Task DeleteCategoryAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<IncomeResponse> GetIncomeAsync(Guid id, CancellationToken cancellationToken);

    Task<IncomeGoalAllocationResponse> GetIncomeGoalAllocationsAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IncomeResponse> CreateIncomeAsync(
        IncomeRequest request,
        CancellationToken cancellationToken);

    Task<IncomeResponse> UpdateIncomeAsync(
        Guid id,
        IncomeRequest request,
        CancellationToken cancellationToken);

    Task DeleteIncomeAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
        DateOnly? from,
        DateOnly? to,
        string? category,
        CancellationToken cancellationToken);

    Task<ExpenseResponse> GetExpenseAsync(Guid id, CancellationToken cancellationToken);

    Task<ExpenseResponse> CreateExpenseAsync(
        ExpenseRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseResponse> UpdateExpenseAsync(
        Guid id,
        ExpenseRequest request,
        CancellationToken cancellationToken);

    Task DeleteExpenseAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecurringTransactionResponse>> GetRecurringTransactionsAsync(
        CancellationToken cancellationToken);

    Task<RecurringTransactionResponse> CreateRecurringTransactionAsync(
        RecurringTransactionRequest request,
        CancellationToken cancellationToken);

    Task<RecurringTransactionResponse> UpdateRecurringTransactionAsync(
        Guid id,
        UpdateRecurringTransactionRequest request,
        CancellationToken cancellationToken);

    Task DeleteRecurringTransactionAsync(Guid id, CancellationToken cancellationToken);

    Task<MonthlyBudgetResponse> GetMonthlyBudgetAsync(
        string? month,
        CancellationToken cancellationToken);

    Task<MonthlyBudgetResponse> SetMonthlyBudgetAsync(
        string month,
        string category,
        BudgetRequest request,
        CancellationToken cancellationToken);

    Task<MonthlyBudgetResponse> DeleteMonthlyBudgetAsync(
        string month,
        string category,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialGoalResponse>> GetFinancialGoalsAsync(
        CancellationToken cancellationToken);

    Task<FinancialGoalResponse> GetFinancialGoalAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<FinancialGoalResponse> CreateFinancialGoalAsync(
        FinancialGoalRequest request,
        CancellationToken cancellationToken);

    Task<FinancialGoalResponse> UpdateFinancialGoalAsync(
        Guid id,
        FinancialGoalRequest request,
        CancellationToken cancellationToken);

    Task DeleteFinancialGoalAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialGoalContributionResponse>> GetFinancialGoalContributionsAsync(
        Guid goalId,
        CancellationToken cancellationToken);

    Task<FinancialGoalContributionResponse> CreateFinancialGoalContributionAsync(
        Guid goalId,
        FinancialGoalContributionRequest request,
        CancellationToken cancellationToken);

    Task DeleteFinancialGoalContributionAsync(
        Guid goalId,
        Guid contributionId,
        CancellationToken cancellationToken);

    Task<CashFlowProjectionResponse> GetCashFlowProjectionAsync(
        int months,
        CancellationToken cancellationToken);
}
