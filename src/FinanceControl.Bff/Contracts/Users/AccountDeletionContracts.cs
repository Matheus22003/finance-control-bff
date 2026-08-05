namespace FinanceControl.Bff.Contracts.Users;

public sealed record DeleteAccountRequest(string Password, string Confirmation);

public sealed record AccountDeletionEligibilityResponse(
    bool CanDelete,
    int OpenDebtsCount,
    int PendingPaymentsCount,
    int ActiveSettlementPlansCount,
    int OwnedGroupsCount,
    IReadOnlyList<string> Blockers);
