using System.Globalization;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Notifications;
using Microsoft.AspNetCore.Identity;

namespace FinanceControl.Bff.Endpoints;

public static class DebtEndpoints
{
    public static RouteGroupBuilder MapDebtEndpoints(this RouteGroupBuilder group)
    {
        MapPeopleEndpoints(group);
        MapDebtsEndpoints(group);
        return group;
    }

    private static void MapPeopleEndpoints(RouteGroupBuilder group)
    {
        var people = group.MapGroup("/people")
            .WithTags("People")
            .RequireAuthorization();

        people.MapGet("/", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetPeopleAsync(cancellationToken)))
            .WithName("GetPeople")
            .Produces<IReadOnlyList<PersonResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        people.MapGet("/{id:guid}", async (
                Guid id,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetPersonAsync(id, cancellationToken)))
            .WithName("GetPersonById")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        people.MapPost("/", async (
                PersonRequest request,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
            {
                var person = await client.CreatePersonAsync(request, cancellationToken);
                return Results.Created($"/api/v1/people/{person.Id}", person);
            })
            .WithName("CreatePerson")
            .Produces<PersonResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        people.MapPut("/{id:guid}", async (
                Guid id,
                PersonRequest request,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.UpdatePersonAsync(id, request, cancellationToken)))
            .WithName("UpdatePerson")
            .Produces<PersonResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        people.MapDelete("/{id:guid}", async (
                Guid id,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
            {
                await client.DeletePersonAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeletePerson")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static void MapDebtsEndpoints(RouteGroupBuilder group)
    {
        var debts = group.MapGroup("/debts")
            .WithTags("Debts")
            .RequireAuthorization();

        debts.MapGet("/summary", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetSummaryAsync(cancellationToken)))
            .WithName("GetDebtSummary")
            .Produces<DebtSummaryResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/settlements/simplified", async (
                Guid? groupId,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetSimplifiedSettlementsAsync(groupId, cancellationToken)))
            .WithName("GetSimplifiedSettlements")
            .Produces<SimplifiedSettlementResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/settlements/simplified/transfers", async (
                Guid? groupId,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetActiveSettlementTransfersAsync(groupId, cancellationToken)))
            .WithName("GetActiveSettlementTransfers")
            .Produces<IReadOnlyList<SettlementTransferResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/settlements/simplified/transfers/pending-confirmation", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetPendingSettlementTransfersAsync(cancellationToken)))
            .WithName("GetPendingSettlementTransferConfirmations")
            .Produces<IReadOnlyList<SettlementTransferResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/settlements/simplified/transfers", async (
                RecordSettlementTransferRequest request,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var transfer = await client.RecordSettlementTransferAsync(request, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    transfer.ToIdentityId == actorUserId ? [] : [transfer.ToIdentityId],
                    NotificationType.SettlementRecorded,
                    "Transferência aguardando confirmação",
                    $"{transfer.FromPerson.Name} registrou {FormatCurrency(transfer.Amount)} para você.",
                    "/debts",
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/debts/settlements/simplified/transfers/{transfer.Id}",
                    transfer);
            })
            .WithName("RecordSettlementTransfer")
            .Produces<SettlementTransferResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/settlements/simplified/transfers/{transferId:guid}/confirm", async (
                Guid transferId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var transfer = await client.ConfirmSettlementTransferAsync(
                    transferId,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    transfer.FromIdentityId == actorUserId ? [] : [transfer.FromIdentityId],
                    NotificationType.SettlementConfirmed,
                    "Transferência confirmada",
                    $"{transfer.ToPerson.Name} confirmou o recebimento de {FormatCurrency(transfer.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(transfer);
            })
            .WithName("ConfirmSettlementTransfer")
            .Produces<SettlementTransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/settlements/simplified/transfers/{transferId:guid}/reject", async (
                Guid transferId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var transfer = await client.RejectSettlementTransferAsync(
                    transferId,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    transfer.FromIdentityId == actorUserId ? [] : [transfer.FromIdentityId],
                    NotificationType.SettlementRejected,
                    "Transferência recusada",
                    $"{transfer.ToPerson.Name} recusou a transferência de {FormatCurrency(transfer.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(transfer);
            })
            .WithName("RejectSettlementTransfer")
            .Produces<SettlementTransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/payments/pending-confirmation", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetPendingConfirmationsAsync(cancellationToken)))
            .WithName("GetPendingPaymentConfirmations")
            .Produces<IReadOnlyList<PaymentResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetDebtsAsync(cancellationToken)))
            .WithName("GetDebts")
            .Produces<IReadOnlyList<DebtResponse>>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/{id:guid}", async (
                Guid id,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetDebtAsync(id, cancellationToken)))
            .WithName("GetDebtById")
            .Produces<DebtResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/", async (
                CreateDebtRequest request,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var debt = await client.CreateDebtAsync(request, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                var recipients = await ResolveDebtRecipientsAsync(
                    debt,
                    actorUserId,
                    userManager,
                    client,
                    cancellationToken);
                await notifications.PublishAsync(
                    recipients,
                    NotificationType.DebtCreated,
                    "Nova dívida compartilhada",
                    $"A dívida {debt.Description} foi adicionada ao seu controle.",
                    "/debts",
                    cancellationToken);
                return Results.Created($"/api/v1/debts/{debt.Id}", debt);
            })
            .WithName("CreateDebt")
            .Produces<DebtResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPut("/{id:guid}", async (
                Guid id,
                UpdateDebtRequest request,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var debt = await client.UpdateDebtAsync(id, request, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                var recipients = await ResolveDebtRecipientsAsync(
                    debt,
                    actorUserId,
                    userManager,
                    client,
                    cancellationToken);
                await notifications.PublishAsync(
                    recipients,
                    NotificationType.DebtUpdated,
                    "Dívida atualizada",
                    $"A dívida {debt.Description} foi atualizada.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(debt);
            })
            .WithName("UpdateDebt")
            .Produces<DebtResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var debt = await client.GetDebtAsync(id, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                var recipients = await ResolveDebtRecipientsAsync(
                    debt,
                    actorUserId,
                    userManager,
                    client,
                    cancellationToken);
                await client.DeleteDebtAsync(id, cancellationToken);
                await notifications.PublishAsync(
                    recipients,
                    NotificationType.DebtDeleted,
                    "Dívida excluída",
                    $"A dívida {debt.Description} foi excluída.",
                    "/debts",
                    cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteDebt")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/{debtId:guid}/payments", async (
                Guid debtId,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetPaymentsAsync(debtId, cancellationToken)))
            .WithName("GetDebtPayments")
            .Produces<IReadOnlyList<PaymentResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/{debtId:guid}/shares/{shareId:guid}/payments", async (
                Guid debtId,
                Guid shareId,
                PaymentRequest request,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var payment = await client.CreatePaymentAsync(
                    debtId,
                    shareId,
                    request,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    payment.ConfirmationRequiredFromUserId is { } recipientId &&
                    recipientId != actorUserId
                        ? [recipientId]
                        : [],
                    NotificationType.PaymentRecorded,
                    "Pagamento aguardando confirmação",
                    $"{payment.FromPerson.Name} registrou um pagamento de {FormatCurrency(payment.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Created($"/api/v1/debts/{debtId}/payments/{payment.Id}", payment);
            })
            .WithName("CreateDebtPayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPut("/{debtId:guid}/payments/{paymentId:guid}", async (
                Guid debtId,
                Guid paymentId,
                PaymentRequest request,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var payment = await client.UpdatePaymentAsync(
                    debtId,
                    paymentId,
                    request,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    payment.ConfirmationRequiredFromUserId is { } recipientId &&
                    recipientId != actorUserId
                        ? [recipientId]
                        : [],
                    NotificationType.PaymentRecorded,
                    "Pagamento atualizado",
                    $"{payment.FromPerson.Name} atualizou o pagamento de {FormatCurrency(payment.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(payment);
            })
            .WithName("UpdateDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/{debtId:guid}/payments/{paymentId:guid}/confirm", async (
                Guid debtId,
                Guid paymentId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var payment = await client.ConfirmPaymentAsync(
                    debtId,
                    paymentId,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    payment.RecordedByUserId == actorUserId ? [] : [payment.RecordedByUserId],
                    NotificationType.PaymentConfirmed,
                    "Pagamento confirmado",
                    $"{payment.ToPerson.Name} confirmou seu pagamento de {FormatCurrency(payment.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(payment);
            })
            .WithName("ConfirmDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapPost("/{debtId:guid}/payments/{paymentId:guid}/reject", async (
                Guid debtId,
                Guid paymentId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var payment = await client.RejectPaymentAsync(
                    debtId,
                    paymentId,
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    payment.RecordedByUserId == actorUserId ? [] : [payment.RecordedByUserId],
                    NotificationType.PaymentRejected,
                    "Pagamento recusado",
                    $"{payment.ToPerson.Name} recusou seu pagamento de {FormatCurrency(payment.Amount)}.",
                    "/debts",
                    cancellationToken);
                return Results.Ok(payment);
            })
            .WithName("RejectDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapDelete("/{debtId:guid}/payments/{paymentId:guid}", async (
                Guid debtId,
                Guid paymentId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var payment = (await client.GetPaymentsAsync(debtId, cancellationToken))
                    .Single(candidate => candidate.Id == paymentId);
                await client.DeletePaymentAsync(debtId, paymentId, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                var recipientId = payment.ConfirmationRequiredFromUserId ?? payment.RecordedByUserId;
                await notifications.PublishAsync(
                    recipientId == actorUserId ? [] : [recipientId],
                    NotificationType.PaymentDeleted,
                    "Pagamento removido",
                    $"O pagamento de {FormatCurrency(payment.Amount)} foi removido.",
                    "/debts",
                    cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteDebtPayment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        debts.MapGet("/{debtId:guid}/history", async (
                Guid debtId,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetHistoryAsync(debtId, cancellationToken)))
            .WithName("GetDebtHistory")
            .Produces<IReadOnlyList<DebtHistoryResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IReadOnlyList<Guid>> ResolveDebtRecipientsAsync(
        DebtResponse debt,
        Guid actorUserId,
        UserManager<ApplicationUser> userManager,
        IDebtServiceClient client,
        CancellationToken cancellationToken)
    {
        if (debt.GroupId is { } groupId)
        {
            var group = await client.GetGroupAsync(groupId, cancellationToken);
            return group.Members
                .Select(member => member.UserId)
                .Where(userId => userId != actorUserId)
                .Distinct()
                .ToList();
        }

        var participantPersonIds = debt.Shares
            .Select(share => share.Person.Id)
            .Append(debt.PaidBy.Id)
            .Distinct()
            .ToHashSet();
        var people = await client.GetPeopleAsync(cancellationToken);
        var recipients = new HashSet<Guid>();
        foreach (var email in people
                     .Where(person => participantPersonIds.Contains(person.Id))
                     .Select(person => person.Email)
                     .Where(email => !string.IsNullOrWhiteSpace(email))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var user = await userManager.FindByEmailAsync(email!);
            if (user is not null && user.Id != actorUserId)
            {
                recipients.Add(user.Id);
            }
        }

        return recipients.ToList();
    }

    private static string FormatCurrency(decimal amount) =>
        amount.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
}
