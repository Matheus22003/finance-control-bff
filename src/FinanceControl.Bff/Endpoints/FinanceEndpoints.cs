using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Endpoints;

public static class FinanceEndpoints
{
    public static RouteGroupBuilder MapFinanceEndpoints(this RouteGroupBuilder group)
    {
        var finance = group.MapGroup("/finance")
            .WithTags("Finance")
            .RequireAuthorization();

        finance.MapGet("/summary", async (
                string? month,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetMonthlySummaryAsync(month, cancellationToken)))
            .WithName("GetFinanceSummary")
            .Produces<FinanceSummaryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/trends", async (
                string? month,
                int? months,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetTrendAsync(month, months ?? 6, cancellationToken)))
            .WithName("GetFinanceTrends")
            .Produces<FinanceTrendResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/categories", async (
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetCategoriesAsync(cancellationToken)))
            .WithName("GetFinanceCategories")
            .Produces<IReadOnlyList<string>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/incomes", async (
                DateOnly? from,
                DateOnly? to,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetIncomesAsync(from, to, cancellationToken)))
            .WithName("GetIncomes")
            .Produces<IReadOnlyList<IncomeResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/incomes/{id:guid}", async (
                Guid id,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetIncomeAsync(id, cancellationToken)))
            .WithName("GetIncomeById")
            .Produces<IncomeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPost("/incomes", async (
                IncomeRequest request,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
            {
                var income = await client.CreateIncomeAsync(request, cancellationToken);
                return Results.Created($"/api/v1/finance/incomes/{income.Id}", income);
            })
            .WithName("CreateIncome")
            .Produces<IncomeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPut("/incomes/{id:guid}", async (
                Guid id,
                IncomeRequest request,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.UpdateIncomeAsync(id, request, cancellationToken)))
            .WithName("UpdateIncome")
            .Produces<IncomeResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapDelete("/incomes/{id:guid}", async (
                Guid id,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
            {
                await client.DeleteIncomeAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteIncome")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/expenses", async (
                DateOnly? from,
                DateOnly? to,
                string? category,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetExpensesAsync(from, to, category, cancellationToken)))
            .WithName("GetExpenses")
            .Produces<IReadOnlyList<ExpenseResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/expenses/{id:guid}", async (
                Guid id,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetExpenseAsync(id, cancellationToken)))
            .WithName("GetExpenseById")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPost("/expenses", async (
                ExpenseRequest request,
                HttpContext context,
                IFinanceServiceClient client,
                BudgetAlertService budgetAlerts,
                CancellationToken cancellationToken) =>
            {
                var month = request.TransactionDate.ToString("yyyy-MM");
                var before = await client.GetMonthlyBudgetAsync(month, cancellationToken);
                var expense = await client.CreateExpenseAsync(request, cancellationToken);
                var after = await client.GetMonthlyBudgetAsync(month, cancellationToken);
                await budgetAlerts.PublishCrossingAsync(
                    AuthenticatedUser.GetId(context.User),
                    before,
                    after,
                    request.Category,
                    cancellationToken);
                return Results.Created($"/api/v1/finance/expenses/{expense.Id}", expense);
            })
            .WithName("CreateExpense")
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPut("/expenses/{id:guid}", async (
                Guid id,
                ExpenseRequest request,
                HttpContext context,
                IFinanceServiceClient client,
                BudgetAlertService budgetAlerts,
                CancellationToken cancellationToken) =>
            {
                var month = request.TransactionDate.ToString("yyyy-MM");
                var before = await client.GetMonthlyBudgetAsync(month, cancellationToken);
                var expense = await client.UpdateExpenseAsync(id, request, cancellationToken);
                var after = await client.GetMonthlyBudgetAsync(month, cancellationToken);
                await budgetAlerts.PublishCrossingAsync(
                    AuthenticatedUser.GetId(context.User),
                    before,
                    after,
                    request.Category,
                    cancellationToken);
                return Results.Ok(expense);
            })
            .WithName("UpdateExpense")
            .Produces<ExpenseResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapDelete("/expenses/{id:guid}", async (
                Guid id,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
            {
                await client.DeleteExpenseAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteExpense")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/recurring-transactions", async (
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetRecurringTransactionsAsync(cancellationToken)))
            .WithName("GetRecurringTransactions")
            .Produces<IReadOnlyList<RecurringTransactionResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPost("/recurring-transactions", async (
                RecurringTransactionRequest request,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
            {
                var recurring = await client.CreateRecurringTransactionAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/v1/finance/recurring-transactions/{recurring.Id}",
                    recurring);
            })
            .WithName("CreateRecurringTransaction")
            .Produces<RecurringTransactionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPut("/recurring-transactions/{id:guid}", async (
                Guid id,
                UpdateRecurringTransactionRequest request,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.UpdateRecurringTransactionAsync(id, request, cancellationToken)))
            .WithName("UpdateRecurringTransaction")
            .Produces<RecurringTransactionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapDelete("/recurring-transactions/{id:guid}", async (
                Guid id,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
            {
                await client.DeleteRecurringTransactionAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteRecurringTransaction")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapGet("/budgets", async (
                string? month,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetMonthlyBudgetAsync(month, cancellationToken)))
            .WithName("GetMonthlyBudget")
            .Produces<MonthlyBudgetResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapPut("/budgets/{category}", async (
                string category,
                string month,
                BudgetRequest request,
                HttpContext context,
                IFinanceServiceClient client,
                BudgetAlertService budgetAlerts,
                CancellationToken cancellationToken) =>
            {
                var before = await client.GetMonthlyBudgetAsync(month, cancellationToken);
                var after = await client.SetMonthlyBudgetAsync(
                    month,
                    category,
                    request,
                    cancellationToken);
                await budgetAlerts.PublishCrossingAsync(
                    AuthenticatedUser.GetId(context.User),
                    before,
                    after,
                    category,
                    cancellationToken);
                return Results.Ok(after);
            })
            .WithName("SetMonthlyBudget")
            .Produces<MonthlyBudgetResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        finance.MapDelete("/budgets/{category}", async (
                string category,
                string month,
                IFinanceServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.DeleteMonthlyBudgetAsync(
                    month,
                    category,
                    cancellationToken)))
            .WithName("DeleteMonthlyBudget")
            .Produces<MonthlyBudgetResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return group;
    }
}
