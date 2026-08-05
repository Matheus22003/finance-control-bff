using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Ai;

public sealed partial class AiQuestionService(
    IFinanceServiceClient financeServiceClient,
    IDebtServiceClient debtServiceClient,
    IAiAnalysisProvider provider,
    TimeProvider timeProvider)
{
    private const int MaximumItemsPerDomain = 250;

    public async Task<AiQuestionResponse> AskAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var incomesTask = financeServiceClient.GetIncomesAsync(cancellationToken);
        var expensesTask = financeServiceClient.GetExpensesAsync(cancellationToken);
        var debtsTask = debtServiceClient.GetDebtsAsync(cancellationToken);
        var groupsTask = debtServiceClient.GetGroupsAsync(cancellationToken);
        var budgetTask = financeServiceClient.GetMonthlyBudgetAsync(null, cancellationToken);
        var trendTask = financeServiceClient.GetTrendAsync(null, 6, cancellationToken);
        await Task.WhenAll(incomesTask, expensesTask, debtsTask, groupsTask, budgetTask, trendTask);

        var incomes = await incomesTask;
        var expenses = await expensesTask;
        var debts = await debtsTask;
        var groups = await groupsTask;
        var budget = await budgetTask;
        var trend = await trendTask;
        var aliases = new AliasRegistry();
        var groupAliases = groups.ToDictionary(
            group => group.Id,
            group => aliases.Add(group.Name, "Grupo"));
        var personAliases = new Dictionary<Guid, string>();

        string PersonAlias(PersonReferenceResponse person)
        {
            if (person.IsCurrentUser)
            {
                return "Você";
            }

            if (personAliases.TryGetValue(person.Id, out var existingAlias))
            {
                return existingAlias;
            }

            var alias = aliases.Add(person.Name, "Pessoa");
            personAliases[person.Id] = alias;
            return alias;
        }

        var transactions = incomes
            .Select(income => new AiTransactionQuestionContext(
                aliases.Add(income.Description, "Lançamento"),
                "INCOME",
                null,
                null,
                income.Amount,
                income.TransactionDate))
            .Concat(expenses.Select(expense => new AiTransactionQuestionContext(
                aliases.Add(expense.Description, "Lançamento"),
                "EXPENSE",
                expense.Category,
                CategoryLabel(expense.Category),
                expense.Amount,
                expense.TransactionDate)))
            .OrderByDescending(transaction => transaction.TransactionDate)
            .Take(MaximumItemsPerDomain)
            .ToList();

        var debtContexts = debts
            .OrderByDescending(debt => debt.CreatedAt)
            .Take(MaximumItemsPerDomain)
            .Select(debt =>
            {
                var paidByAlias = PersonAlias(debt.PaidBy);
                var participants = debt.Shares
                    .Select(share => new AiDebtParticipantContext(
                        PersonAlias(share.Person),
                        share.Amount,
                        share.PaidAmount,
                        share.RemainingAmount,
                        share.IsPayer,
                        share.Person.IsCurrentUser))
                    .ToList();
                var currentUserOwes = debt.PaidBy.IsCurrentUser
                    ? 0m
                    : debt.Shares
                        .Where(share => share.Person.IsCurrentUser)
                        .Sum(share => share.RemainingAmount);
                var owedToCurrentUser = debt.PaidBy.IsCurrentUser
                    ? debt.Shares
                        .Where(share => !share.Person.IsCurrentUser)
                        .Sum(share => share.RemainingAmount)
                    : 0m;

                return new AiDebtQuestionContext(
                    aliases.Add(debt.Description, "Dívida"),
                    debt.Category,
                    CategoryLabel(debt.Category),
                    debt.GroupId.HasValue
                        ? groupAliases.GetValueOrDefault(debt.GroupId.Value, "Grupo")
                        : "Sem grupo",
                    debt.TotalAmount,
                    currentUserOwes,
                    owedToCurrentUser,
                    owedToCurrentUser > 0m
                        ? "OWED_TO_CURRENT_USER"
                        : currentUserOwes > 0m
                            ? "CURRENT_USER_OWES"
                            : "NO_OPEN_POSITION",
                    paidByAlias,
                    debt.Status,
                    debt.DueDate,
                    debt.CreatedAt,
                    participants);
            })
            .ToList();

        var receivables = debtContexts
            .Where(debt => debt.PositionDirection == "OWED_TO_CURRENT_USER")
            .SelectMany(debt => debt.Participants
                .Where(participant =>
                    !participant.IsCurrentUser &&
                    !participant.IsPayer &&
                    participant.RemainingAmount > 0m)
                .Select(participant => new
                {
                    participant.PersonAlias,
                    debt.Category,
                    debt.CategoryLabel,
                    Amount = participant.RemainingAmount
                }))
            .GroupBy(item => new { item.PersonAlias, item.Category, item.CategoryLabel })
            .Select(group => new AiReceivableQuestionContext(
                group.Key.PersonAlias,
                group.Key.Category,
                group.Key.CategoryLabel,
                group.Sum(item => item.Amount)))
            .OrderByDescending(receivable => receivable.Amount)
            .ToList();
        var payablesByCategory = debtContexts
            .Where(debt => debt.CurrentUserOwes > 0m)
            .GroupBy(debt => new { debt.Category, debt.CategoryLabel })
            .Select(group => new AiPayableCategoryQuestionContext(
                group.Key.Category,
                group.Key.CategoryLabel,
                group.Sum(debt => debt.CurrentUserOwes)))
            .OrderByDescending(payable => payable.Amount)
            .ToList();

        var context = new AiQuestionContext(
            aliases.Sanitize(question),
            timeProvider.GetUtcNow(),
            incomes.Count + expenses.Count > MaximumItemsPerDomain ||
            debts.Count > MaximumItemsPerDomain,
            transactions,
            debtContexts,
            receivables,
            payablesByCategory,
            budget.Categories
                .Where(item => item.Planned > 0m)
                .Select(item => new AiBudgetCategoryContext(
                    item.Category,
                    item.Planned,
                    item.Spent,
                    item.Remaining,
                    item.UsagePercentage))
                .ToList(),
            trend.Items.Select(item => new AiMonthlyTrendContext(
                item.ReferenceMonth,
                item.TotalIncome,
                item.TotalExpenses,
                item.Balance)).ToList());
        var response = TryAnswerDeterministically(context) ??
                       await provider.AskAsync(context, cancellationToken);

        return response with
        {
            Answer = aliases.Restore(response.Answer),
            SuggestedQuestions = response.SuggestedQuestions
                .Select(aliases.Restore)
                .ToList()
        };
    }

    private AiQuestionResponse? TryAnswerDeterministically(AiQuestionContext context)
    {
        var normalizedQuestion = RemoveDiacritics(context.Question).ToLowerInvariant();
        var category = DetectCategory(normalizedQuestion);
        var suggestions = new[]
        {
            "Quem ainda me deve dinheiro?",
            "De onde acumulei minhas dívidas?",
            "Quanto gastei com alimentação?"
        };

        if (normalizedQuestion.Contains("quem") &&
            (normalizedQuestion.Contains("me deve") ||
             normalizedQuestion.Contains("a receber")))
        {
            var receivables = context.Receivables
                .Where(item => category is null || item.Category == category)
                .GroupBy(item => item.PersonAlias)
                .Select(group => new
                {
                    PersonAlias = group.Key,
                    Amount = group.Sum(item => item.Amount)
                })
                .OrderByDescending(item => item.Amount)
                .ToList();
            var answer = receivables.Count == 0
                ? category is null
                    ? "Não encontrei valores pendentes que outras pessoas devam a você."
                    : $"Não encontrei valores pendentes de {CategoryLabel(category).ToLowerInvariant()} que outras pessoas devam a você."
                : $"{JoinWithAnd(receivables.Select(item => $"{item.PersonAlias} ainda deve {Money(item.Amount)}"))}{(category is null ? string.Empty : $" em {CategoryLabel(category).ToLowerInvariant()}")}.";
            return new AiQuestionResponse(
                timeProvider.GetUtcNow(),
                "deterministic",
                answer,
                suggestions);
        }

        if ((normalizedQuestion.Contains("de onde") ||
             normalizedQuestion.Contains("origem") ||
             normalizedQuestion.Contains("acumulei")) &&
            normalizedQuestion.Contains("divid"))
        {
            var answer = context.PayablesByCategory.Count == 0
                ? "Você não possui valores a pagar nas dívidas atuais."
                : $"Seus valores a pagar vêm principalmente de {JoinWithAnd(context.PayablesByCategory.Select(item => $"{item.CategoryLabel}: {Money(item.Amount)}"))}.";
            return new AiQuestionResponse(
                timeProvider.GetUtcNow(),
                "deterministic",
                answer,
                suggestions);
        }

        if ((normalizedQuestion.Contains("quanto") || normalizedQuestion.Contains("total")) &&
            (normalizedQuestion.Contains("gastei") ||
             normalizedQuestion.Contains("gasto") ||
             normalizedQuestion.Contains("despesa")))
        {
            var expenses = context.Transactions
                .Where(transaction => transaction.Kind == "EXPENSE" &&
                                      (category is null || transaction.Category == category))
                .ToList();
            var answer = expenses.Count == 0
                ? category is null
                    ? "Não encontrei despesas registradas."
                    : $"Não encontrei despesas de {CategoryLabel(category).ToLowerInvariant()} registradas."
                : $"Você gastou {Money(expenses.Sum(expense => expense.Amount))}{(category is null ? string.Empty : $" com {CategoryLabel(category).ToLowerInvariant()}")} nos lançamentos disponíveis.";
            return new AiQuestionResponse(
                timeProvider.GetUtcNow(),
                "deterministic",
                answer,
                suggestions);
        }

        if (normalizedQuestion.Contains("orcamento") || normalizedQuestion.Contains("limite"))
        {
            var budgets = context.BudgetCategories
                .Where(item => category is null || item.Category == category)
                .OrderByDescending(item => item.UsagePercentage)
                .ToList();
            var answer = budgets.Count == 0
                ? category is null
                    ? "Você ainda não definiu orçamentos para este mês."
                    : $"Você ainda não definiu um orçamento para {CategoryLabel(category).ToLowerInvariant()}."
                : JoinWithAnd(budgets.Select(item =>
                    $"{CategoryLabel(item.Category)} está em {item.UsagePercentage:N0}% ({Money(item.Spent)} de {Money(item.Planned)})")) + ".";
            return new AiQuestionResponse(
                timeProvider.GetUtcNow(),
                "deterministic",
                answer,
                suggestions);
        }

        return null;
    }

    private static string CategoryLabel(string category) => category switch
    {
        "FOOD" => "Alimentação",
        "TRANSPORT" => "Transporte",
        "RENT" => "Moradia",
        "LEISURE" => "Lazer",
        "HEALTH" => "Saúde",
        "TRAVEL" => "Viagens",
        "LOAN" => "Empréstimos",
        _ => "Outros"
    };

    private static string? DetectCategory(string question)
    {
        if (question.Contains("comida") || question.Contains("aliment"))
        {
            return "FOOD";
        }

        if (question.Contains("transporte"))
        {
            return "TRANSPORT";
        }

        if (question.Contains("aluguel") || question.Contains("moradia"))
        {
            return "RENT";
        }

        if (question.Contains("lazer"))
        {
            return "LEISURE";
        }

        if (question.Contains("saude"))
        {
            return "HEALTH";
        }

        if (question.Contains("viagem"))
        {
            return "TRAVEL";
        }

        if (question.Contains("emprestimo"))
        {
            return "LOAN";
        }

        return null;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string JoinWithAnd(IEnumerable<string> values)
    {
        var items = values.ToList();
        return items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            2 => $"{items[0]} e {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Count - 1))} e {items[^1]}"
        };
    }

    private static string Money(decimal value) =>
        value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    private sealed partial class AliasRegistry
    {
        private readonly Dictionary<string, string> _aliasesByOriginal =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _originalsByAlias =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _counters =
            new(StringComparer.OrdinalIgnoreCase);

        public string Add(string? original, string prefix)
        {
            if (string.IsNullOrWhiteSpace(original))
            {
                return prefix;
            }

            var normalized = original.Trim();
            if (_aliasesByOriginal.TryGetValue(normalized, out var existing))
            {
                return existing;
            }

            var next = _counters.GetValueOrDefault(prefix) + 1;
            _counters[prefix] = next;
            var alias = $"{prefix} {next}";
            _aliasesByOriginal[normalized] = alias;
            _originalsByAlias[alias] = normalized;
            return alias;
        }

        public string Sanitize(string value)
        {
            var sanitized = _aliasesByOriginal
                .OrderByDescending(entry => entry.Key.Length)
                .Aggregate(value, (current, entry) =>
                    Regex.Replace(
                        current,
                        Regex.Escape(entry.Key),
                        _ => entry.Value,
                        RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(100)));
            return EmailRegex().Replace(sanitized, "[e-mail removido]");
        }

        public string Restore(string value) => _originalsByAlias
            .OrderByDescending(entry => entry.Key.Length)
            .Aggregate(value, (current, entry) =>
                Regex.Replace(
                    current,
                    $@"\b{Regex.Escape(entry.Key)}\b",
                    _ => entry.Value,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(100)));

        [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();
    }
}
