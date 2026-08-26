namespace FinanceControl.Bff.Notifications;

public sealed record NotificationTypeDefinition(
    NotificationType Type,
    string ContractValue,
    string Category,
    string Label);

public static class NotificationTypeCatalog
{
    public static readonly IReadOnlyList<NotificationTypeDefinition> All =
    [
        Define(NotificationType.FriendRequest, "SOCIAL", "Novo convite de amizade"),
        Define(NotificationType.FriendAccepted, "SOCIAL", "Convite de amizade aceito"),
        Define(NotificationType.FriendRejected, "SOCIAL", "Convite de amizade recusado"),
        Define(NotificationType.FriendRemoved, "SOCIAL", "Amizade removida"),
        Define(NotificationType.GroupCreated, "SOCIAL", "Grupo criado"),
        Define(NotificationType.GroupUpdated, "SOCIAL", "Grupo atualizado"),
        Define(NotificationType.GroupMemberAdded, "SOCIAL", "Pessoa adicionada ao grupo"),
        Define(NotificationType.GroupMemberRemoved, "SOCIAL", "Pessoa removida do grupo"),
        Define(NotificationType.GroupDeleted, "SOCIAL", "Grupo excluído"),
        Define(NotificationType.DebtCreated, "DEBTS", "Dívida criada"),
        Define(NotificationType.DebtUpdated, "DEBTS", "Dívida atualizada"),
        Define(NotificationType.DebtDeleted, "DEBTS", "Dívida excluída"),
        Define(NotificationType.PaymentRecorded, "DEBTS", "Pagamento registrado"),
        Define(NotificationType.PaymentConfirmed, "DEBTS", "Pagamento confirmado"),
        Define(NotificationType.PaymentRejected, "DEBTS", "Pagamento recusado"),
        Define(NotificationType.PaymentDeleted, "DEBTS", "Pagamento excluído"),
        Define(NotificationType.SettlementRecorded, "DEBTS", "Acerto registrado"),
        Define(NotificationType.SettlementConfirmed, "DEBTS", "Acerto confirmado"),
        Define(NotificationType.SettlementRejected, "DEBTS", "Acerto recusado"),
        Define(NotificationType.BudgetWarning, "FINANCE", "Orçamento próximo do limite"),
        Define(NotificationType.BudgetExceeded, "FINANCE", "Orçamento ultrapassado"),
        Define(NotificationType.GoalDueSoon, "FINANCE", "Meta próxima do prazo"),
        Define(NotificationType.GoalOverdue, "FINANCE", "Meta atrasada"),
        Define(NotificationType.GoalCompleted, "FINANCE", "Meta concluída")
    ];

    private static readonly IReadOnlyDictionary<string, NotificationType> ByContractValue = All
        .ToDictionary(definition => definition.ContractValue, definition => definition.Type,
            StringComparer.OrdinalIgnoreCase);

    public static string ToContractValue(NotificationType type) =>
        All.Single(definition => definition.Type == type).ContractValue;

    public static bool TryParse(string? value, out NotificationType type) =>
        ByContractValue.TryGetValue(value?.Trim() ?? string.Empty, out type);

    private static NotificationTypeDefinition Define(
        NotificationType type,
        string category,
        string label) =>
        new(type, ToSnakeCase(type), category, label);

    private static string ToSnakeCase(NotificationType type) =>
        string.Concat(type.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $"_{character}"
                : character.ToString())).ToUpperInvariant();
}
