namespace FinanceControl.Bff.Clients.Debt;

public interface IDebtServiceClient
{
    Task<AccountDeletionEligibilityResponse> GetAccountDeletionEligibilityAsync(
        CancellationToken cancellationToken);

    Task DeleteAccountDataAsync(CancellationToken cancellationToken);

    Task UpdateUserSnapshotAsync(
        UserSnapshotRequest request,
        CancellationToken cancellationToken);

    Task<DebtSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);

    Task<DebtAnalysisContextResponse> GetAnalysisContextAsync(
        CancellationToken cancellationToken);

    Task<DebtReportResponse> GetReportAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<SimplifiedSettlementResponse> GetSimplifiedSettlementsAsync(
        Guid? groupId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SettlementTransferResponse>> GetActiveSettlementTransfersAsync(
        Guid? groupId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SettlementTransferResponse>> GetPendingSettlementTransfersAsync(
        CancellationToken cancellationToken);

    Task<SettlementTransferResponse> RecordSettlementTransferAsync(
        RecordSettlementTransferRequest request,
        CancellationToken cancellationToken);

    Task<SettlementTransferResponse> ConfirmSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken);

    Task<SettlementTransferResponse> RejectSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonResponse>> GetPeopleAsync(CancellationToken cancellationToken);

    Task<PersonResponse> GetPersonAsync(Guid id, CancellationToken cancellationToken);

    Task<PersonResponse> CreatePersonAsync(
        PersonRequest request,
        CancellationToken cancellationToken);

    Task<PersonResponse> UpdatePersonAsync(
        Guid id,
        PersonRequest request,
        CancellationToken cancellationToken);

    Task DeletePersonAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DebtResponse>> GetDebtsAsync(CancellationToken cancellationToken);

    Task<DebtResponse> GetDebtAsync(Guid id, CancellationToken cancellationToken);

    Task<DebtResponse> CreateDebtAsync(
        CreateDebtRequest request,
        CancellationToken cancellationToken);

    Task<DebtResponse> UpdateDebtAsync(
        Guid id,
        UpdateDebtRequest request,
        CancellationToken cancellationToken);

    Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentResponse>> GetPaymentsAsync(
        Guid debtId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentResponse>> GetPendingConfirmationsAsync(
        CancellationToken cancellationToken);

    Task<PaymentResponse> CreatePaymentAsync(
        Guid debtId,
        Guid shareId,
        PaymentRequest request,
        CancellationToken cancellationToken);

    Task<PaymentResponse> UpdatePaymentAsync(
        Guid debtId,
        Guid paymentId,
        PaymentRequest request,
        CancellationToken cancellationToken);

    Task<PaymentResponse> ConfirmPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken);

    Task<PaymentResponse> RejectPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken);

    Task DeletePaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DebtHistoryResponse>> GetHistoryAsync(
        Guid debtId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FriendshipResponse>> GetIncomingFriendRequestsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FriendshipResponse>> GetOutgoingFriendRequestsAsync(
        CancellationToken cancellationToken);

    Task<FriendshipResponse> CreateFriendRequestAsync(
        CreateFriendRequest request,
        CancellationToken cancellationToken);

    Task<FriendshipResponse> AcceptFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<FriendshipResponse> RejectFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task RemoveFriendAsync(Guid friendUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupResponse>> GetGroupsAsync(CancellationToken cancellationToken);

    Task<GroupResponse> GetGroupAsync(Guid groupId, CancellationToken cancellationToken);

    Task<GroupResponse> CreateGroupAsync(
        CreateGroupServiceRequest request,
        CancellationToken cancellationToken);

    Task<GroupResponse> UpdateGroupAsync(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken);

    Task<GroupResponse> AddGroupMemberAsync(
        Guid groupId,
        AddGroupMemberServiceRequest request,
        CancellationToken cancellationToken);

    Task RemoveGroupMemberAsync(
        Guid groupId,
        Guid memberUserId,
        CancellationToken cancellationToken);

    Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken);
}
