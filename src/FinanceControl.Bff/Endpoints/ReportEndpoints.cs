using System.Globalization;
using System.Text;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Reports;

namespace FinanceControl.Bff.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        reports.MapGet("/overview", GetOverviewAsync)
            .WithName("GetReportOverview")
            .Produces<ReportOverviewResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        reports.MapGet("/export.csv", ExportCsvAsync)
            .WithName("ExportReportCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return group;
    }

    private static async Task<IResult> GetOverviewAsync(
        string? from,
        string? to,
        IFinanceServiceClient financeClient,
        IDebtServiceClient debtClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var resolution = ResolvePeriod(from, to, timeProvider);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        return Results.Ok(await BuildOverviewAsync(
            resolution.Period!, financeClient, debtClient, timeProvider, cancellationToken));
    }

    private static async Task<IResult> ExportCsvAsync(
        string? from,
        string? to,
        IFinanceServiceClient financeClient,
        IDebtServiceClient debtClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var resolution = ResolvePeriod(from, to, timeProvider);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var report = await BuildOverviewAsync(
            resolution.Period!, financeClient, debtClient, timeProvider, cancellationToken);
        var csv = BuildCsv(report);
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv);
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);
        return Results.File(
            bytes,
            "text/csv; charset=utf-8",
            $"finance-control-relatorio-{report.FromMonth}-a-{report.ToMonth}.csv");
    }

    private static async Task<ReportOverviewResponse> BuildOverviewAsync(
        ReportPeriod period,
        IFinanceServiceClient financeClient,
        IDebtServiceClient debtClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var financeTask = financeClient.GetReportAsync(
            period.FromMonth, period.ToMonth, cancellationToken);
        var debtTask = debtClient.GetReportAsync(
            period.FromDate, period.ToDate, cancellationToken);
        await Task.WhenAll(financeTask, debtTask);
        var finance = await financeTask;
        var debts = await debtTask;
        var financeMonths = finance.Months
            .Select(month => new ReportFinanceMonth(
                month.ReferenceMonth,
                month.TotalIncome,
                month.TotalExpenses,
                month.Balance))
            .ToList();

        return new ReportOverviewResponse(
            period.FromMonth,
            period.ToMonth,
            period.MonthCount,
            timeProvider.GetUtcNow(),
            new ReportFinanceSection(
                finance.TotalIncome,
                finance.TotalExpenses,
                finance.Balance,
                finance.SavingsRatePercentage,
                finance.IncomeCount,
                finance.ExpenseCount,
                financeMonths,
                finance.ExpenseCategories.Select(category => new ReportFinanceCategory(
                    category.Category,
                    category.Name,
                    category.Amount,
                    category.Percentage)).ToList(),
                finance.TopExpenses.Select(expense => new ReportExpenseItem(
                    expense.Id,
                    expense.Description,
                    expense.Amount,
                    expense.TransactionDate,
                    expense.Category,
                    expense.CategoryName)).ToList()),
            new ReportDebtSection(
                debts.TotalVolume,
                debts.TotalOwed,
                debts.TotalToReceive,
                debts.OpenDebtsCount,
                debts.PaidDebtsCount,
                debts.Months.Select(month => new ReportDebtMonth(
                    month.ReferenceMonth,
                    month.TotalVolume,
                    month.TotalOwed,
                    month.TotalToReceive,
                    month.DebtCount)).ToList(),
                debts.Categories.Select(category => new ReportDebtCategory(
                    category.Category,
                    category.TotalVolume,
                    category.TotalOwed,
                    category.TotalToReceive,
                    category.DebtCount)).ToList(),
                debts.TopDebts.Select(debt => new ReportDebtItem(
                    debt.Id,
                    debt.Description,
                    debt.Category,
                    debt.TotalAmount,
                    debt.TotalOwed,
                    debt.TotalToReceive,
                    debt.Status,
                    debt.DueDate,
                    debt.CreatedAt)).ToList()),
            new ReportHighlights(
                RoundCurrency(finance.TotalIncome / period.MonthCount),
                RoundCurrency(finance.TotalExpenses / period.MonthCount),
                financeMonths.MaxBy(month => month.Balance)?.ReferenceMonth,
                finance.ExpenseCategories.MaxBy(category => category.Amount)?.Name));
    }

    private static string BuildCsv(ReportOverviewResponse report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Secao;Referencia;Receitas;Despesas;Saldo;A pagar;A receber;Volume");
        var debtMonths = report.Debts.Months.ToDictionary(month => month.ReferenceMonth);
        foreach (var month in report.Finance.Months)
        {
            debtMonths.TryGetValue(month.ReferenceMonth, out var debt);
            AppendRow(builder,
                "Mensal",
                month.ReferenceMonth,
                month.TotalIncome,
                month.TotalExpenses,
                month.Balance,
                debt?.TotalOwed ?? 0m,
                debt?.TotalToReceive ?? 0m,
                debt?.TotalVolume ?? 0m);
        }

        foreach (var category in report.Finance.ExpenseCategories)
        {
            AppendRow(builder, "Categoria financeira", category.Name, 0m, category.Amount, -category.Amount, 0m, 0m, 0m);
        }

        foreach (var category in report.Debts.Categories)
        {
            AppendRow(builder, "Categoria de divida", category.Category, 0m, 0m, 0m, category.TotalOwed, category.TotalToReceive, category.TotalVolume);
        }

        return builder.ToString();
    }

    private static void AppendRow(
        StringBuilder builder,
        string section,
        string reference,
        decimal income,
        decimal expenses,
        decimal balance,
        decimal owed,
        decimal receivable,
        decimal volume)
    {
        builder.Append(EscapeCsv(section)).Append(';')
            .Append(EscapeCsv(reference)).Append(';')
            .Append(FormatDecimal(income)).Append(';')
            .Append(FormatDecimal(expenses)).Append(';')
            .Append(FormatDecimal(balance)).Append(';')
            .Append(FormatDecimal(owed)).Append(';')
            .Append(FormatDecimal(receivable)).Append(';')
            .Append(FormatDecimal(volume)).AppendLine();
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static PeriodResolution ResolvePeriod(
        string? from,
        string? to,
        TimeProvider timeProvider)
    {
        var currentDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var defaultTo = new DateOnly(currentDate.Year, currentDate.Month, 1);
        if (!TryParseMonth(to, defaultTo, out var toMonth) ||
            !TryParseMonth(from, toMonth.AddMonths(-5), out var fromMonth))
        {
            return PeriodResolution.Invalid("Use the yyyy-MM format for from and to.");
        }

        if (fromMonth > toMonth)
        {
            return PeriodResolution.Invalid("From month must be on or before to month.");
        }

        var monthCount = (toMonth.Year - fromMonth.Year) * 12 + toMonth.Month - fromMonth.Month + 1;
        if (monthCount > 24)
        {
            return PeriodResolution.Invalid("Reports support a maximum period of 24 months.");
        }

        return PeriodResolution.Valid(new ReportPeriod(
            fromMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            toMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            fromMonth,
            toMonth.AddMonths(1).AddDays(-1),
            monthCount));
    }

    private static bool TryParseMonth(string? value, DateOnly fallback, out DateOnly month)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            month = fallback;
            return true;
        }

        return DateOnly.TryParseExact(
            $"{value}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out month);
    }

    private sealed record ReportPeriod(
        string FromMonth,
        string ToMonth,
        DateOnly FromDate,
        DateOnly ToDate,
        int MonthCount);

    private sealed record PeriodResolution(ReportPeriod? Period, IResult? Error)
    {
        public static PeriodResolution Valid(ReportPeriod period) => new(period, null);

        public static PeriodResolution Invalid(string message) => new(
            null,
            Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["period"] = [message]
            }));
    }
}
