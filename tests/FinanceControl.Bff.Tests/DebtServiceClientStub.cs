using FinanceControl.Bff.Clients.Debt;

namespace FinanceControl.Bff.Tests;

internal abstract class DebtServiceClientStub : IDebtServiceClient
{
    public virtual Task<AccountDeletionEligibilityResponse> GetAccountDeletionEligibilityAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(new AccountDeletionEligibilityResponse(true, 0, 0, 0, 0, []));

    public virtual Task DeleteAccountDataAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public virtual Task UpdateUserSnapshotAsync(
        UserSnapshotRequest request,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual Task<DebtSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<DebtAnalysisContextResponse> GetAnalysisContextAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<DebtReportResponse> GetReportAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        Task.FromResult(new DebtReportResponse(
            from,
            to,
            0m,
            0m,
            0m,
            0,
            0,
            [],
            [],
            []));

    public virtual Task<SimplifiedSettlementResponse> GetSimplifiedSettlementsAsync(
        Guid? groupId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<SettlementTransferResponse>> GetActiveSettlementTransfersAsync(
        Guid? groupId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<SettlementTransferResponse>> GetPendingSettlementTransfersAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SettlementTransferResponse> RecordSettlementTransferAsync(
        RecordSettlementTransferRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SettlementTransferResponse> ConfirmSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SettlementTransferResponse> RejectSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<PersonResponse>> GetPeopleAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PersonResponse> GetPersonAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PersonResponse> CreatePersonAsync(
        PersonRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PersonResponse> UpdatePersonAsync(
        Guid id,
        PersonRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeletePersonAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<DebtResponse>> GetDebtsAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<DebtResponse> GetDebtAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<DebtResponse> CreateDebtAsync(
        CreateDebtRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<DebtResponse> UpdateDebtAsync(
        Guid id,
        UpdateDebtRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<PaymentResponse>> GetPaymentsAsync(
        Guid debtId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<PaymentResponse>> GetPendingConfirmationsAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PaymentResponse> CreatePaymentAsync(
        Guid debtId,
        Guid shareId,
        PaymentRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PaymentResponse> UpdatePaymentAsync(
        Guid debtId,
        Guid paymentId,
        PaymentRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PaymentResponse> ConfirmPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PaymentResponse> RejectPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task DeletePaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<DebtHistoryResponse>> GetHistoryAsync(
        Guid debtId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<IReadOnlyList<FriendshipResponse>> GetIncomingFriendRequestsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<IReadOnlyList<FriendshipResponse>> GetOutgoingFriendRequestsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<FriendshipResponse> CreateFriendRequestAsync(
        CreateFriendRequest request,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<FriendshipResponse> AcceptFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<FriendshipResponse> RejectFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task RemoveFriendAsync(
        Guid friendUserId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<IReadOnlyList<GroupResponse>> GetGroupsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<GroupResponse> GetGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<GroupResponse> CreateGroupAsync(
        CreateGroupServiceRequest request,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<GroupResponse> UpdateGroupAsync(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<GroupResponse> AddGroupMemberAsync(
        Guid groupId,
        AddGroupMemberServiceRequest request,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task RemoveGroupMemberAsync(
        Guid groupId,
        Guid memberUserId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task DeleteGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
