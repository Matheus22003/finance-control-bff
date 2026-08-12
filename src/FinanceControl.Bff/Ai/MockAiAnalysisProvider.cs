using FinanceControl.Bff.Contracts.Ai;

namespace FinanceControl.Bff.Ai;

public sealed class MockAiAnalysisProvider(TimeProvider timeProvider) : IAiAnalysisProvider
{
    public string Name => "mock";

    public Task<AiAnalysisResponse> AnalyzeAsync(
        AiAnalysisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var financeInsights = BuildFinanceInsights(context);
        var debtInsights = BuildDebtInsights(context);
        var recommendations = BuildRecommendations(context);
        var overview = BuildOverview(context);

        return Task.FromResult(new AiAnalysisResponse(
            timeProvider.GetUtcNow(),
            Name,
            context.ReferenceMonth,
            overview,
            new AiAnalysisMetrics(
                context.TotalIncome,
                context.TotalExpenses,
                context.Balance,
                context.TotalOwed,
                context.TotalToReceive,
                context.OpenDebtsCount,
                context.OverdueDebtsCount,
                context.DueSoonDebtsCount,
                context.OriginalTransferCount,
                context.SimplifiedTransferCount),
            financeInsights,
            debtInsights,
            recommendations));
    }

    public Task<AiQuestionResponse> AskAsync(
        AiQuestionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var category = DetectCategory(context.Question);
        var peopleWhoOwe = context.Receivables
            .Where(receivable => category is null || receivable.Category == category)
            .GroupBy(receivable => receivable.PersonAlias)
            .Select(group => new
            {
                Person = group.Key,
                Amount = group.Sum(receivable => receivable.Amount)
            })
            .OrderByDescending(item => item.Amount)
            .ToList();

        string answer;
        if (peopleWhoOwe.Count > 0 &&
            (context.Question.Contains("deve", StringComparison.OrdinalIgnoreCase) ||
             context.Question.Contains("receber", StringComparison.OrdinalIgnoreCase)))
        {
            answer = string.Join(
                " ",
                peopleWhoOwe.Select(item => $"{item.Person} ainda deve {Money(item.Amount)}."));
        }
        else
        {
            var debtsByCategory = context.PayablesByCategory;
            answer = debtsByCategory.Count == 0
                ? "Não encontrei dívidas a pagar nos dados disponíveis."
                : $"Sua maior origem de dívida é {CategoryLabel(debtsByCategory[0].Category)}, com {Money(debtsByCategory[0].Amount)} em aberto.";
        }

        return Task.FromResult(new AiQuestionResponse(
            timeProvider.GetUtcNow(),
            Name,
            answer,
            [
                "Quem ainda me deve dinheiro?",
                "Em qual categoria acumulei mais dívidas?",
                "Quanto gastei com alimentação?",
                "Consigo atingir minha meta?"
            ]));
    }

    private static IReadOnlyList<AiInsightResponse> BuildFinanceInsights(AiAnalysisContext context)
    {
        var insights = new List<AiInsightResponse>();
        if (context.TotalIncome == 0m && context.TotalExpenses == 0m)
        {
            insights.Add(new AiInsightResponse(
                "INFO",
                "Período sem lançamentos",
                "Ainda não há receitas ou despesas registradas no mês analisado."));
            return insights;
        }

        var savingsRate = context.TotalIncome > 0m
            ? context.Balance / context.TotalIncome
            : 0m;
        insights.Add(context.Balance >= 0m
            ? new AiInsightResponse(
                savingsRate >= 0.2m ? "POSITIVE" : "INFO",
                "Resultado mensal positivo",
                $"O saldo do período é {Money(context.Balance)}, equivalente a {savingsRate:P0} das receitas.")
            : new AiInsightResponse(
                "WARNING",
                "Despesas acima das receitas",
                $"As saídas superaram as entradas em {Money(decimal.Abs(context.Balance))}."));

        var largestCategory = context.ExpenseCategories.FirstOrDefault();
        if (largestCategory is not null && context.TotalExpenses > 0m)
        {
            insights.Add(new AiInsightResponse(
                "INFO",
                "Maior categoria de despesa",
                $"{CategoryLabel(largestCategory.Category)} concentrou {largestCategory.Amount / context.TotalExpenses:P0} das despesas ({Money(largestCategory.Amount)})."));
        }

        var budgetAtRisk = context.BudgetCategories
            .OrderByDescending(category => category.UsagePercentage)
            .FirstOrDefault(category => category.UsagePercentage >= 80m);
        if (budgetAtRisk is not null)
        {
            insights.Add(new AiInsightResponse(
                budgetAtRisk.UsagePercentage >= 100m ? "CRITICAL" : "WARNING",
                budgetAtRisk.UsagePercentage >= 100m
                    ? "Limite mensal excedido"
                    : "Orçamento próximo do limite",
                $"{CategoryLabel(budgetAtRisk.Category)} consumiu {budgetAtRisk.UsagePercentage:N0}% do valor planejado ({Money(budgetAtRisk.Spent)} de {Money(budgetAtRisk.Planned)})."));
        }

        var activeGoal = context.Goals
            .Where(goal => goal.Status != "COMPLETED")
            .OrderBy(goal => goal.TargetDate)
            .FirstOrDefault();
        if (activeGoal is not null)
        {
            insights.Add(new AiInsightResponse(
                activeGoal.Status == "OVERDUE" ? "WARNING" : "INFO",
                activeGoal.Status == "OVERDUE" ? "Meta com prazo vencido" : "Progresso da meta",
                $"{activeGoal.Alias} está em {activeGoal.ProgressPercentage:N0}% e precisa de aproximadamente {Money(activeGoal.RequiredMonthlyContribution)} por mês."));
        }

        if (context.CashFlowProjection.ProjectedCumulativeBalance < 0m)
        {
            insights.Add(new AiInsightResponse(
                "WARNING",
                "Projeção de caixa negativa",
                $"O fluxo acumulado projetado para os próximos meses é {Money(context.CashFlowProjection.ProjectedCumulativeBalance)}."));
        }

        return insights;
    }

    private static IReadOnlyList<AiInsightResponse> BuildDebtInsights(AiAnalysisContext context)
    {
        var insights = new List<AiInsightResponse>();
        if (context.OpenDebtsCount == 0)
        {
            insights.Add(new AiInsightResponse(
                "POSITIVE",
                "Nenhuma dívida em aberto",
                "Não há valores pendentes a pagar ou receber neste momento."));
            return insights;
        }

        insights.Add(new AiInsightResponse(
            "INFO",
            "Posição nas dívidas",
            $"Você tem {Money(context.TotalOwed)} a pagar e {Money(context.TotalToReceive)} a receber em {context.OpenDebtsCount} dívida(s)."));

        if (context.OverdueDebtsCount > 0)
        {
            insights.Add(new AiInsightResponse(
                "CRITICAL",
                "Há compromissos vencidos",
                $"{context.OverdueDebtsCount} dívida(s) a pagar ultrapassaram a data de vencimento."));
        }
        else if (context.DueSoonDebtsCount > 0)
        {
            insights.Add(new AiInsightResponse(
                "WARNING",
                "Vencimentos próximos",
                $"{context.DueSoonDebtsCount} dívida(s) a pagar vencem nos próximos sete dias."));
        }

        var largestCategory = context.DebtCategories.FirstOrDefault();
        if (largestCategory is not null)
        {
            insights.Add(new AiInsightResponse(
                "INFO",
                "Principal origem das dívidas",
                $"{CategoryLabel(largestCategory.Category)} representa o maior impacto: {Money(largestCategory.TotalOwed)} a pagar e {Money(largestCategory.TotalToReceive)} a receber."));
        }

        var largestGroup = context.DebtGroups.FirstOrDefault();
        if (largestGroup is not null)
        {
            insights.Add(new AiInsightResponse(
                "INFO",
                "Maior concentração por grupo",
                $"{largestGroup.Alias} concentra {Money(largestGroup.TotalOwed + largestGroup.TotalToReceive)} da sua posição em aberto."));
        }

        return insights;
    }

    private static IReadOnlyList<string> BuildRecommendations(AiAnalysisContext context)
    {
        var recommendations = new List<string>();
        if (context.BudgetCategories.Any(category => category.UsagePercentage >= 80m))
        {
            recommendations.Add("Revise as categorias próximas ou acima do limite antes de novos gastos no mês.");
        }


        if (context.Goals.Any(goal => goal.Status == "OVERDUE"))
        {
            recommendations.Add("Revise o prazo ou o valor mensal das metas atrasadas para torná-las alcançáveis.");
        }
        if (context.OverdueDebtsCount > 0)
        {
            recommendations.Add("Priorize os compromissos vencidos e confirme com os envolvidos antes de registrar pagamentos.");
        }

        if (context.Balance < 0m)
        {
            recommendations.Add("Revise a maior categoria de despesa para recuperar margem no próximo período.");
        }
        else if (context.TotalIncome > 0m && context.Balance / context.TotalIncome < 0.2m)
        {
            recommendations.Add("Considere reservar uma parte do saldo antes de assumir novos compromissos compartilhados.");
        }

        if (context.OriginalTransferCount > context.SimplifiedTransferCount)
        {
            var avoided = context.OriginalTransferCount - context.SimplifiedTransferCount;
            recommendations.Add($"Use o plano simplificado: ele evita {avoided} transferência(s) desnecessária(s).");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Continue registrando receitas, despesas e pagamentos para manter a análise atualizada.");
        }

        return recommendations;
    }

    private static string BuildOverview(AiAnalysisContext context)
    {
        var monthlyPosition = context.Balance >= 0m
            ? $"saldo positivo de {Money(context.Balance)}"
            : $"déficit de {Money(decimal.Abs(context.Balance))}";
        var debtPosition = context.TotalToReceive - context.TotalOwed;
        var debtText = debtPosition >= 0m
            ? $"posição líquida a receber de {Money(debtPosition)}"
            : $"posição líquida a pagar de {Money(decimal.Abs(debtPosition))}";

        return $"No período {context.ReferenceMonth}, você apresenta {monthlyPosition} e {debtText}.";
    }

    private static string Money(decimal value) =>
        value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

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
        if (question.Contains("comida", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("aliment", StringComparison.OrdinalIgnoreCase))
        {
            return "FOOD";
        }

        if (question.Contains("transporte", StringComparison.OrdinalIgnoreCase))
        {
            return "TRANSPORT";
        }

        if (question.Contains("aluguel", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("moradia", StringComparison.OrdinalIgnoreCase))
        {
            return "RENT";
        }

        return null;
    }
}
