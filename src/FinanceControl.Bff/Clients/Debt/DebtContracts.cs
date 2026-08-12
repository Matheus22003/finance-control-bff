namespace FinanceControl.Bff.Clients.Debt;

public sealed record AccountDeletionEligibilityResponse(
    bool CanDelete,
    int OpenDebtsCount,
    int PendingPaymentsCount,
    int ActiveSettlementPlansCount,
    int OwnedGroupsCount,
    IReadOnlyList<string> Blockers);

public sealed record DebtAnalysisContextResponse(
    DateTimeOffset GeneratedAt,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int PaidDebtsCount,
    int OverdueDebtsCount,
    int DueSoonDebtsCount,
    IReadOnlyList<DebtAnalysisCategoryResponse> Categories,
    IReadOnlyList<DebtAnalysisGroupResponse> Groups,
    IReadOnlyList<DebtAnalysisDriverResponse> TopDrivers);

public sealed record DebtAnalysisCategoryResponse(
    string Category,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record DebtAnalysisGroupResponse(
    Guid? GroupId,
    string? GroupName,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record DebtAnalysisDriverResponse(
    string Category,
    Guid? GroupId,
    string? GroupName,
    decimal TotalOwed,
    decimal TotalToReceive,
    DateOnly? DueDate,
    bool IsOverdue);

public sealed record PersonRequest(
    string Name,
    string? Email,
    bool IsCurrentUser);

public sealed record PersonResponse(
    Guid Id,
    string Name,
    string? Email,
    bool IsCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateDebtRequest(
    string Description,
    decimal TotalAmount,
    Guid PaidByPersonId,
    Guid? GroupId,
    string Category,
    DateOnly? DueDate,
    IReadOnlyList<DebtShareRequest> Shares);

public sealed record DebtShareRequest(
    Guid PersonId,
    decimal Amount);

public sealed record UpdateDebtRequest(
    string Description,
    Guid PaidByPersonId,
    string Category,
    DateOnly? DueDate,
    IReadOnlyList<DebtShareRequest> Shares);

public sealed record DebtResponse(
    Guid Id,
    string Description,
    decimal TotalAmount,
    PersonReferenceResponse PaidBy,
    Guid? GroupId,
    string Category,
    string Status,
    DateOnly? DueDate,
    bool CreatedByCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DebtShareResponse> Shares);

public sealed record PersonReferenceResponse(
    Guid Id,
    string Name,
    bool IsCurrentUser);

public sealed record DebtShareResponse(
    Guid Id,
    PersonReferenceResponse Person,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsPayer);

public sealed record PaymentRequest(
    decimal Amount,
    DateOnly PaymentDate,
    string? Note);

public sealed record PaymentResponse(
    Guid Id,
    Guid DebtId,
    Guid DebtShareId,
    PersonReferenceResponse FromPerson,
    PersonReferenceResponse ToPerson,
    decimal Amount,
    DateOnly PaymentDate,
    string? Note,
    Guid RecordedByUserId,
    Guid? ConfirmationRequiredFromUserId,
    string Status,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    bool CanConfirm,
    bool CanReject,
    bool CanEdit,
    bool CanDelete,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DebtHistoryResponse(
    Guid Id,
    string Type,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record SimplifiedSettlementResponse(
    decimal TotalOpenAmount,
    int OriginalTransferCount,
    int SimplifiedTransferCount,
    IReadOnlyList<SimplifiedTransferResponse> Transfers);

public sealed record SimplifiedTransferResponse(
    Guid FromIdentityId,
    PersonReferenceResponse FromPerson,
    Guid ToIdentityId,
    PersonReferenceResponse ToPerson,
    decimal Amount);

public sealed record RecordSettlementTransferRequest(
    Guid? GroupId,
    Guid FromPersonId,
    Guid ToPersonId,
    decimal Amount,
    DateOnly PaymentDate,
    string? Note);

public sealed record SettlementTransferResponse(
    Guid Id,
    Guid SettlementPlanId,
    Guid? GroupId,
    Guid FromIdentityId,
    PersonReferenceResponse FromPerson,
    Guid ToIdentityId,
    PersonReferenceResponse ToPerson,
    decimal Amount,
    DateOnly? PaymentDate,
    string? Note,
    string Status,
    bool CanRecord,
    bool CanConfirm,
    bool CanReject,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
