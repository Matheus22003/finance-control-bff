using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Contracts.Users;

public sealed record AccountExportResponse(
    DateTimeOffset ExportedAt,
    AccountProfileExport Profile,
    IReadOnlyList<IncomeResponse> Incomes,
    IReadOnlyList<ExpenseResponse> Expenses,
    IReadOnlyList<PersonResponse> People,
    IReadOnlyList<DebtExportResponse> Debts,
    IReadOnlyList<FriendResponse> Friends,
    IReadOnlyList<FriendshipResponse> IncomingFriendRequests,
    IReadOnlyList<FriendshipResponse> OutgoingFriendRequests,
    IReadOnlyList<GroupResponse> Groups,
    IReadOnlyList<NotificationResponse> Notifications,
    IReadOnlyList<AccountSessionExport> Sessions);

public sealed record AccountProfileExport(
    Guid Id,
    string DisplayName,
    string Email,
    bool EmailConfirmed,
    bool HasAvatar,
    UserPreferencesResponse Preferences);

public sealed record DebtExportResponse(
    DebtResponse Debt,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<DebtHistoryResponse> History);

public sealed record AccountSessionExport(
    Guid Id,
    string DeviceName,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);
