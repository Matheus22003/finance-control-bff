using System.Net;
using System.Text;
using System.Text.Json;
using FinanceControl.Bff.Clients.Debt;

namespace FinanceControl.Bff.Tests;

public sealed class DebtServiceClientTests
{
    [Fact]
    public async Task GetSummaryAsync_DeserializesDebtServiceContract()
    {
        const string json = """
                            {
                              "totalOwed": 420.00,
                              "totalToReceive": 180.00,
                              "openDebtsCount": 3
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var summary = await client.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(420.00m, summary.TotalOwed);
        Assert.Equal(180.00m, summary.TotalToReceive);
        Assert.Equal(3, summary.OpenDebtsCount);
        Assert.Equal("/api/v1/debts/summary", handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task AccountDeletionLifecycle_UsesInternalAccountEndpoints()
    {
        const string json = """
                            {
                              "canDelete": true,
                              "openDebtsCount": 0,
                              "pendingPaymentsCount": 0,
                              "activeSettlementPlansCount": 0,
                              "ownedGroupsCount": 0,
                              "blockers": []
                            }
                            """;
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        var eligibility = await client.GetAccountDeletionEligibilityAsync(CancellationToken.None);
        await client.DeleteAccountDataAsync(CancellationToken.None);

        Assert.True(eligibility.CanDelete);
        Assert.Equal(
            [
                (HttpMethod.Get, "/api/v1/internal/account-data/deletion-eligibility"),
                (HttpMethod.Delete, "/api/v1/internal/account-data")
            ],
            requests);
    }

    [Fact]
    public async Task GetSimplifiedSettlementsAsync_ForwardsTheSelectedGroupScope()
    {
        var groupId = Guid.Parse("b79a7335-e28d-4bdc-baf6-f990aa66ce31");
        const string json = """
                            {
                              "totalOpenAmount": 150.00,
                              "originalTransferCount": 3,
                              "simplifiedTransferCount": 2,
                              "transfers": []
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var settlement = await client.GetSimplifiedSettlementsAsync(
            groupId,
            CancellationToken.None);

        Assert.Equal(2, settlement.SimplifiedTransferCount);
        Assert.Equal("/api/v1/debts/settlements/simplified", handler.LastRequestUri?.AbsolutePath);
        Assert.Equal($"?groupId={groupId:D}", handler.LastRequestUri?.Query);
    }

    [Fact]
    public async Task RecordSettlementTransferAsync_ForwardsTheProtectedCommand()
    {
        var groupId = Guid.Parse("b79a7335-e28d-4bdc-baf6-f990aa66ce31");
        var transferId = Guid.Parse("bcb633eb-1aec-47f9-a192-019930c71a11");
        var planId = Guid.Parse("02918629-97f0-480c-bef3-7cc951ea5ce2");
        var fromPersonId = Guid.Parse("bd902fae-39b3-49d9-b67c-6580f900d36d");
        var toPersonId = Guid.Parse("6a675716-46a1-48c7-9307-441700cd498e");
        var json = $$"""
                     {
                       "id": "{{transferId}}",
                       "settlementPlanId": "{{planId}}",
                       "groupId": "{{groupId}}",
                       "fromIdentityId": "{{fromPersonId}}",
                       "fromPerson": { "id": "{{fromPersonId}}", "name": "Ana", "isCurrentUser": true },
                       "toIdentityId": "{{toPersonId}}",
                       "toPerson": { "id": "{{toPersonId}}", "name": "Bruno", "isCurrentUser": false },
                       "amount": 30.00,
                       "paymentDate": "2026-08-03",
                       "note": "PIX",
                       "status": "PENDING",
                       "canRecord": false,
                       "canConfirm": false,
                       "canReject": false,
                       "confirmedAt": null,
                       "rejectedAt": null,
                       "createdAt": "2026-08-03T12:00:00Z",
                       "updatedAt": "2026-08-03T12:00:00Z"
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var transfer = await client.RecordSettlementTransferAsync(
            new RecordSettlementTransferRequest(
                groupId,
                fromPersonId,
                toPersonId,
                30m,
                new DateOnly(2026, 8, 3),
                "PIX"),
            CancellationToken.None);

        Assert.Equal("PENDING", transfer.Status);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(
            "/api/v1/debts/settlements/simplified/transfers",
            handler.LastRequestUri?.AbsolutePath);
        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal(groupId, requestBody.RootElement.GetProperty("groupId").GetGuid());
        Assert.Equal(30m, requestBody.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task CreateDebtAsync_ForwardsSharedExpenseContract()
    {
        var payerId = Guid.Parse("2ed65935-53a7-4596-8cc5-7c8ce95354e1");
        var participantId = Guid.Parse("5fded6c9-38b1-490d-bfe8-8d5fe0ae0ef8");
        var debtId = Guid.Parse("2130ec47-c28c-4b9e-a194-322487bbd353");
        var shareId = Guid.Parse("991be9e7-d776-48d2-8841-c151dd9ac97c");
        var json = $$"""
                     {
                       "id": "{{debtId}}",
                       "description": "Jantar",
                       "totalAmount": 120.00,
                       "paidBy": { "id": "{{payerId}}", "name": "Eu", "isCurrentUser": true },
                       "category": "FOOD",
                       "status": "OPEN",
                       "dueDate": null,
                       "createdAt": "2026-07-31T12:00:00Z",
                       "updatedAt": "2026-07-31T12:00:00Z",
                       "shares": [
                         {
                           "id": "{{shareId}}",
                           "person": { "id": "{{participantId}}", "name": "Ana", "isCurrentUser": false },
                           "amount": 120.00,
                           "paidAmount": 0,
                           "remainingAmount": 120.00,
                           "isPayer": false
                         }
                       ]
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var request = new CreateDebtRequest(
            "Jantar",
            120.00m,
            payerId,
            null,
            "FOOD",
            null,
            [new DebtShareRequest(participantId, 120.00m)]);

        var debt = await client.CreateDebtAsync(request, CancellationToken.None);

        Assert.Equal(debtId, debt.Id);
        Assert.Equal("OPEN", debt.Status);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/v1/debts", handler.LastRequestUri?.AbsolutePath);

        using var requestBody = JsonDocument.Parse(Assert.IsType<string>(handler.LastRequestBody));
        Assert.Equal(payerId, requestBody.RootElement.GetProperty("paidByPersonId").GetGuid());
        Assert.Equal("FOOD", requestBody.RootElement.GetProperty("category").GetString());
        Assert.Equal(
            participantId,
            requestBody.RootElement.GetProperty("shares")[0].GetProperty("personId").GetGuid());
    }

    [Fact]
    public async Task DeletePersonAsync_MapsConflictProblemDetails()
    {
        const string json = """
                            {
                              "title": "Person is in use",
                              "status": 409,
                              "detail": "The person participates in one or more debts."
                            }
                            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<DebtServiceException>(() =>
            client.DeletePersonAsync(Guid.Empty, CancellationToken.None));

        Assert.Equal(DebtServiceFailure.Rejected, exception.Failure);
        Assert.Equal(409, exception.UpstreamStatusCode);
        Assert.Equal("Person is in use", exception.UpstreamProblem?.Title);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_ForwardsCommandAndDeserializesConfirmationState()
    {
        var debtId = Guid.Parse("2130ec47-c28c-4b9e-a194-322487bbd353");
        var paymentId = Guid.Parse("d7183eb4-34a5-493f-8f89-68c29b755e18");
        var shareId = Guid.Parse("991be9e7-d776-48d2-8841-c151dd9ac97c");
        var userId = Guid.Parse("7f805b46-0b56-4a5d-86eb-d4f53c92db93");
        var personId = Guid.Parse("5fded6c9-38b1-490d-bfe8-8d5fe0ae0ef8");
        var json = $$"""
                     {
                       "id": "{{paymentId}}",
                       "debtId": "{{debtId}}",
                       "debtShareId": "{{shareId}}",
                       "fromPerson": { "id": "{{personId}}", "name": "Ana", "isCurrentUser": false },
                       "toPerson": { "id": "{{userId}}", "name": "Eu", "isCurrentUser": true },
                       "amount": 50.00,
                       "paymentDate": "2026-08-01",
                       "note": "PIX",
                       "recordedByUserId": "{{personId}}",
                       "confirmationRequiredFromUserId": "{{userId}}",
                       "status": "CONFIRMED",
                       "confirmedAt": "2026-08-01T12:00:00Z",
                       "rejectedAt": null,
                       "canConfirm": false,
                       "canReject": false,
                       "canEdit": false,
                       "canDelete": false,
                       "createdAt": "2026-08-01T11:59:00Z",
                       "updatedAt": "2026-08-01T12:00:00Z"
                     }
                     """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var payment = await client.ConfirmPaymentAsync(debtId, paymentId, CancellationToken.None);

        Assert.Equal("CONFIRMED", payment.Status);
        Assert.NotNull(payment.ConfirmedAt);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(
            $"/api/v1/debts/{debtId}/payments/{paymentId}/confirm",
            handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetPendingConfirmationsAsync_UsesProtectedDebtServiceRoute()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var payments = await client.GetPendingConfirmationsAsync(CancellationToken.None);

        Assert.Empty(payments);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal(
            "/api/v1/debts/payments/pending-confirmation",
            handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetSummaryAsync_MapsUpstreamHttpError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<DebtServiceException>(
            () => client.GetSummaryAsync(CancellationToken.None));

        Assert.Equal(DebtServiceFailure.InvalidResponse, exception.Failure);
        Assert.Equal(500, exception.UpstreamStatusCode);
    }

    [Fact]
    public async Task GetSummaryAsync_MapsConnectionFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<DebtServiceException>(
            () => client.GetSummaryAsync(CancellationToken.None));

        Assert.Equal(DebtServiceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task GetSummaryAsync_MapsMalformedJson()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{invalid-json", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<DebtServiceException>(
            () => client.GetSummaryAsync(CancellationToken.None));

        Assert.Equal(DebtServiceFailure.InvalidResponse, exception.Failure);
    }

    private static DebtServiceClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://debt-service.test"),
            Timeout = TimeSpan.FromSeconds(2)
        };

        return new DebtServiceClient(httpClient);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
