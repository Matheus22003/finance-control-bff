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

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<IncomeResponse> GetIncomeAsync(Guid id, CancellationToken cancellationToken);

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
}
