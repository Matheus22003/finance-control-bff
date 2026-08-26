using System.Net.Http.Json;
using FinanceControl.Bff.Clients;

namespace FinanceControl.Bff.Clients.Debt;

public sealed class DebtServiceClient(HttpClient httpClient) : IDebtServiceClient
{
    private const string DebtsPath = "/api/v1/debts";
    private const string PeoplePath = "/api/v1/people";
    private const string FriendsPath = "/api/v1/friends";
    private const string GroupsPath = "/api/v1/groups";
    private const string UserSnapshotsPath = "/api/v1/internal/user-snapshots";
    private const string AccountDataPath = "/api/v1/internal/account-data";

    public Task<AccountDeletionEligibilityResponse> GetAccountDeletionEligibilityAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<AccountDeletionEligibilityResponse>(
            HttpMethod.Get,
            $"{AccountDataPath}/deletion-eligibility",
            null,
            cancellationToken);

    public Task DeleteAccountDataAsync(CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            AccountDataPath,
            cancellationToken);

    public Task UpdateUserSnapshotAsync(
        UserSnapshotRequest request,
        CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Put,
            $"{UserSnapshotsPath}/{request.UserId}",
            request,
            cancellationToken);

    public Task<DebtSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtSummaryResponse>(
            HttpMethod.Get,
            $"{DebtsPath}/summary",
            null,
            cancellationToken);

    public Task<DebtAnalysisContextResponse> GetAnalysisContextAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtAnalysisContextResponse>(
            HttpMethod.Get,
            $"{DebtsPath}/analysis-context",
            null,
            cancellationToken);

    public Task<DebtReportResponse> GetReportAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtReportResponse>(
            HttpMethod.Get,
            $"{DebtsPath}/reports/overview?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            null,
            cancellationToken);

    public Task<SimplifiedSettlementResponse> GetSimplifiedSettlementsAsync(
        Guid? groupId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<SimplifiedSettlementResponse>(
            HttpMethod.Get,
            groupId is null
                ? $"{DebtsPath}/settlements/simplified"
                : $"{DebtsPath}/settlements/simplified?groupId={groupId:D}",
            null,
            cancellationToken);

    public Task<IReadOnlyList<SettlementTransferResponse>> GetActiveSettlementTransfersAsync(
        Guid? groupId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<SettlementTransferResponse>>(
            HttpMethod.Get,
            groupId is null
                ? $"{DebtsPath}/settlements/simplified/transfers"
                : $"{DebtsPath}/settlements/simplified/transfers?groupId={groupId:D}",
            null,
            cancellationToken);

    public Task<IReadOnlyList<SettlementTransferResponse>> GetPendingSettlementTransfersAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<SettlementTransferResponse>>(
            HttpMethod.Get,
            $"{DebtsPath}/settlements/simplified/transfers/pending-confirmation",
            null,
            cancellationToken);

    public Task<SettlementTransferResponse> RecordSettlementTransferAsync(
        RecordSettlementTransferRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<SettlementTransferResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/settlements/simplified/transfers",
            request,
            cancellationToken);

    public Task<SettlementTransferResponse> ConfirmSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<SettlementTransferResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/settlements/simplified/transfers/{transferId}/confirm",
            null,
            cancellationToken);

    public Task<SettlementTransferResponse> RejectSettlementTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<SettlementTransferResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/settlements/simplified/transfers/{transferId}/reject",
            null,
            cancellationToken);

    public Task<IReadOnlyList<PersonResponse>> GetPeopleAsync(CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<PersonResponse>>(
            HttpMethod.Get,
            PeoplePath,
            null,
            cancellationToken);

    public Task<PersonResponse> GetPersonAsync(Guid id, CancellationToken cancellationToken) =>
        SendForJsonAsync<PersonResponse>(
            HttpMethod.Get,
            $"{PeoplePath}/{id}",
            null,
            cancellationToken);

    public Task<PersonResponse> CreatePersonAsync(
        PersonRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PersonResponse>(HttpMethod.Post, PeoplePath, request, cancellationToken);

    public Task<PersonResponse> UpdatePersonAsync(
        Guid id,
        PersonRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PersonResponse>(
            HttpMethod.Put,
            $"{PeoplePath}/{id}",
            request,
            cancellationToken);

    public Task DeletePersonAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(HttpMethod.Delete, $"{PeoplePath}/{id}", cancellationToken);

    public Task<IReadOnlyList<DebtResponse>> GetDebtsAsync(CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<DebtResponse>>(
            HttpMethod.Get,
            DebtsPath,
            null,
            cancellationToken);

    public Task<DebtResponse> GetDebtAsync(Guid id, CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtResponse>(
            HttpMethod.Get,
            $"{DebtsPath}/{id}",
            null,
            cancellationToken);

    public Task<DebtResponse> CreateDebtAsync(
        CreateDebtRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtResponse>(HttpMethod.Post, DebtsPath, request, cancellationToken);

    public Task<DebtResponse> UpdateDebtAsync(
        Guid id,
        UpdateDebtRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<DebtResponse>(
            HttpMethod.Put,
            $"{DebtsPath}/{id}",
            request,
            cancellationToken);

    public Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(HttpMethod.Delete, $"{DebtsPath}/{id}", cancellationToken);

    public Task<IReadOnlyList<PaymentResponse>> GetPaymentsAsync(
        Guid debtId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<PaymentResponse>>(
            HttpMethod.Get,
            $"{DebtsPath}/{debtId}/payments",
            null,
            cancellationToken);

    public Task<IReadOnlyList<PaymentResponse>> GetPendingConfirmationsAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<PaymentResponse>>(
            HttpMethod.Get,
            $"{DebtsPath}/payments/pending-confirmation",
            null,
            cancellationToken);

    public Task<PaymentResponse> CreatePaymentAsync(
        Guid debtId,
        Guid shareId,
        PaymentRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PaymentResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/{debtId}/shares/{shareId}/payments",
            request,
            cancellationToken);

    public Task<PaymentResponse> UpdatePaymentAsync(
        Guid debtId,
        Guid paymentId,
        PaymentRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PaymentResponse>(
            HttpMethod.Put,
            $"{DebtsPath}/{debtId}/payments/{paymentId}",
            request,
            cancellationToken);

    public Task<PaymentResponse> ConfirmPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PaymentResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/{debtId}/payments/{paymentId}/confirm",
            null,
            cancellationToken);

    public Task<PaymentResponse> RejectPaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<PaymentResponse>(
            HttpMethod.Post,
            $"{DebtsPath}/{debtId}/payments/{paymentId}/reject",
            null,
            cancellationToken);

    public Task DeletePaymentAsync(
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{DebtsPath}/{debtId}/payments/{paymentId}",
            cancellationToken);

    public Task<IReadOnlyList<DebtHistoryResponse>> GetHistoryAsync(
        Guid debtId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<DebtHistoryResponse>>(
            HttpMethod.Get,
            $"{DebtsPath}/{debtId}/history",
            null,
            cancellationToken);

    public Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FriendResponse>>(HttpMethod.Get, FriendsPath, null, cancellationToken);

    public Task<IReadOnlyList<FriendshipResponse>> GetIncomingFriendRequestsAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FriendshipResponse>>(
            HttpMethod.Get,
            $"{FriendsPath}/requests/incoming",
            null,
            cancellationToken);

    public Task<IReadOnlyList<FriendshipResponse>> GetOutgoingFriendRequestsAsync(
        CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<FriendshipResponse>>(
            HttpMethod.Get,
            $"{FriendsPath}/requests/outgoing",
            null,
            cancellationToken);

    public Task<FriendshipResponse> CreateFriendRequestAsync(
        CreateFriendRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FriendshipResponse>(
            HttpMethod.Post,
            $"{FriendsPath}/requests",
            request,
            cancellationToken);

    public Task<FriendshipResponse> AcceptFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FriendshipResponse>(
            HttpMethod.Post,
            $"{FriendsPath}/requests/{requestId}/accept",
            null,
            cancellationToken);

    public Task<FriendshipResponse> RejectFriendRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<FriendshipResponse>(
            HttpMethod.Post,
            $"{FriendsPath}/requests/{requestId}/reject",
            null,
            cancellationToken);

    public Task RemoveFriendAsync(Guid friendUserId, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{FriendsPath}/{friendUserId}",
            cancellationToken);

    public Task<IReadOnlyList<GroupResponse>> GetGroupsAsync(CancellationToken cancellationToken) =>
        SendForJsonAsync<IReadOnlyList<GroupResponse>>(HttpMethod.Get, GroupsPath, null, cancellationToken);

    public Task<GroupResponse> GetGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        SendForJsonAsync<GroupResponse>(HttpMethod.Get, $"{GroupsPath}/{groupId}", null, cancellationToken);

    public Task<GroupResponse> CreateGroupAsync(
        CreateGroupServiceRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<GroupResponse>(HttpMethod.Post, GroupsPath, request, cancellationToken);

    public Task<GroupResponse> UpdateGroupAsync(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<GroupResponse>(
            HttpMethod.Put,
            $"{GroupsPath}/{groupId}",
            request,
            cancellationToken);

    public Task<GroupResponse> AddGroupMemberAsync(
        Guid groupId,
        AddGroupMemberServiceRequest request,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<GroupResponse>(
            HttpMethod.Post,
            $"{GroupsPath}/{groupId}/members",
            request,
            cancellationToken);

    public Task RemoveGroupMemberAsync(
        Guid groupId,
        Guid memberUserId,
        CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(
            HttpMethod.Delete,
            $"{GroupsPath}/{groupId}/members/{memberUserId}",
            cancellationToken);

    public Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        SendWithoutResponseBodyAsync(HttpMethod.Delete, $"{GroupsPath}/{groupId}", cancellationToken);

    private async Task<TResponse> SendForJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        object? requestBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (requestBody is not null)
        {
            request.Content = JsonContent.Create(requestBody);
        }

        using var response = await SendAsync(request, cancellationToken);
        return await UpstreamResponseReader.ReadRequiredJsonAsync<TResponse>(
            response,
            "Debt Service",
            (message, exception) => new DebtServiceException(
                DebtServiceFailure.InvalidResponse,
                message,
                exception),
            cancellationToken);
    }

    private async Task SendWithoutResponseBodyAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken) =>
        await SendWithoutResponseBodyAsync(method, path, null, cancellationToken);

    private async Task SendWithoutResponseBodyAsync(
        HttpMethod method,
        string path,
        object? requestBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (requestBody is not null)
        {
            request.Content = JsonContent.Create(requestBody);
        }
        using var response = await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = (int)response.StatusCode;
            var problem = await UpstreamResponseReader.ReadProblemDetailsAsync(response, cancellationToken);
            response.Dispose();

            throw new DebtServiceException(
                statusCode is >= 400 and < 500
                    ? DebtServiceFailure.Rejected
                    : DebtServiceFailure.InvalidResponse,
                $"Debt Service returned HTTP {statusCode}.",
                upstreamStatusCode: statusCode,
                upstreamProblem: problem);
        }
        catch (DebtServiceException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DebtServiceException(
                DebtServiceFailure.Timeout,
                "Debt Service request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new DebtServiceException(
                DebtServiceFailure.Unavailable,
                "Debt Service could not be reached.",
                exception);
        }
    }
}
