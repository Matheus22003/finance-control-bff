using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Endpoints;

public static class SocialEndpoints
{
    public static RouteGroupBuilder MapSocialEndpoints(this RouteGroupBuilder group)
    {
        MapFriends(group);
        MapGroups(group);
        return group;
    }

    private static void MapFriends(RouteGroupBuilder group)
    {
        var friends = group.MapGroup("/friends").WithTags("Friends").RequireAuthorization();

        friends.MapGet("/", async (IDebtServiceClient client, CancellationToken cancellationToken) =>
                Results.Ok(await client.GetFriendsAsync(cancellationToken)))
            .Produces<IReadOnlyList<FriendResponse>>();

        friends.MapGet("/requests/incoming", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetIncomingFriendRequestsAsync(cancellationToken)))
            .Produces<IReadOnlyList<FriendshipResponse>>();

        friends.MapGet("/requests/outgoing", async (
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetOutgoingFriendRequestsAsync(cancellationToken)))
            .Produces<IReadOnlyList<FriendshipResponse>>();

        friends.MapPost("/requests", async (
                SendFriendRequest request,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["email"] = ["Email is required."]
                    });
                }

                var requester = await UserEndpoints.FindCurrentAsync(context.User, userManager);
                var target = await userManager.FindByEmailAsync(request.Email.Trim());
                if (target is null || target.Id == requester.Id)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "User not found",
                        detail: "No other Finance Control user was found with this email.");
                }

                var created = await client.CreateFriendRequestAsync(
                    new CreateFriendRequest(
                        target.Id,
                        requester.DisplayName,
                        requester.Email!,
                        target.DisplayName,
                        target.Email!),
                    cancellationToken);
                await notifications.PublishAsync(
                    [target.Id],
                    NotificationType.FriendRequest,
                    "Novo pedido de amizade",
                    $"{requester.DisplayName} enviou um pedido de amizade.",
                    "/social",
                    cancellationToken);
                return Results.Created($"/api/v1/friends/requests/{created.Id}", created);
            })
            .Produces<FriendshipResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        friends.MapPost("/requests/{requestId:guid}/accept", async (
                Guid requestId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var accepted = await client.AcceptFriendRequestAsync(requestId, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    accepted.RequesterUserId == actorUserId ? [] : [accepted.RequesterUserId],
                    NotificationType.FriendAccepted,
                    "Pedido de amizade aceito",
                    $"{accepted.AddresseeDisplayName} aceitou seu pedido de amizade.",
                    "/social",
                    cancellationToken);
                return Results.Ok(accepted);
            })
            .Produces<FriendshipResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        friends.MapPost("/requests/{requestId:guid}/reject", async (
                Guid requestId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var rejected = await client.RejectFriendRequestAsync(requestId, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    rejected.RequesterUserId == actorUserId ? [] : [rejected.RequesterUserId],
                    NotificationType.FriendRejected,
                    "Pedido de amizade recusado",
                    $"{rejected.AddresseeDisplayName} recusou seu pedido de amizade.",
                    "/social",
                    cancellationToken);
                return Results.Ok(rejected);
            })
            .Produces<FriendshipResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        friends.MapDelete("/{friendUserId:guid}", async (
                Guid friendUserId,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var currentUser = await UserEndpoints.FindCurrentAsync(context.User, userManager);
                await client.RemoveFriendAsync(friendUserId, cancellationToken);
                await notifications.PublishAsync(
                    [friendUserId],
                    NotificationType.FriendRemoved,
                    "Amizade removida",
                    $"{currentUser.DisplayName} removeu a amizade.",
                    "/social",
                    cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapGroups(RouteGroupBuilder group)
    {
        var groups = group.MapGroup("/groups").WithTags("Groups").RequireAuthorization();

        groups.MapGet("/", async (IDebtServiceClient client, CancellationToken cancellationToken) =>
                Results.Ok(await client.GetGroupsAsync(cancellationToken)))
            .Produces<IReadOnlyList<GroupResponse>>();

        groups.MapGet("/{groupId:guid}", async (
                Guid groupId,
                IDebtServiceClient client,
                CancellationToken cancellationToken) =>
                Results.Ok(await client.GetGroupAsync(groupId, cancellationToken)))
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapPost("/", async (
                CreateGroupRequest request,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var owner = await UserEndpoints.FindCurrentAsync(context.User, userManager);
                var memberIds = (request.MemberUserIds ?? [])
                    .Where(id => id != owner.Id)
                    .Distinct()
                    .ToList();
                var members = await userManager.Users
                    .Where(user => memberIds.Contains(user.Id))
                    .ToListAsync(cancellationToken);
                if (members.Count != memberIds.Count)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["memberUserIds"] = ["One or more users do not exist."]
                    });
                }

                var created = await client.CreateGroupAsync(
                    new CreateGroupServiceRequest(
                        request.Name,
                        request.Description,
                        ToSnapshot(owner),
                        members.Select(ToSnapshot).ToList()),
                    cancellationToken);
                await notifications.PublishAsync(
                    memberIds,
                    NotificationType.GroupCreated,
                    "Você entrou em um grupo",
                    $"{owner.DisplayName} adicionou você ao grupo {created.Name}.",
                    "/social",
                    cancellationToken);
                return Results.Created($"/api/v1/groups/{created.Id}", created);
            })
            .Produces<GroupResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        groups.MapPut("/{groupId:guid}", async (
                Guid groupId,
                HttpContext context,
                UpdateGroupRequest request,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var updated = await client.UpdateGroupAsync(groupId, request, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    updated.Members.Select(member => member.UserId).Where(id => id != actorUserId),
                    NotificationType.GroupUpdated,
                    "Grupo atualizado",
                    $"O grupo {updated.Name} foi atualizado.",
                    "/social",
                    cancellationToken);
                return Results.Ok(updated);
            })
            .Produces<GroupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapPost("/{groupId:guid}/members", async (
                Guid groupId,
                AddGroupMemberRequest request,
                HttpContext context,
                UserManager<ApplicationUser> userManager,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var user = await userManager.FindByIdAsync(request.UserId.ToString());
                if (user is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["userId"] = ["The user does not exist."]
                    });
                }

                var updated = await client.AddGroupMemberAsync(
                    groupId,
                    new AddGroupMemberServiceRequest(ToSnapshot(user)),
                    cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    updated.Members.Select(member => member.UserId).Where(id => id != actorUserId),
                    NotificationType.GroupMemberAdded,
                    "Novo participante no grupo",
                    $"{user.DisplayName} entrou no grupo {updated.Name}.",
                    "/social",
                    cancellationToken);
                return Results.Ok(updated);
            })
            .Produces<GroupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapDelete("/{groupId:guid}/members/{memberUserId:guid}", async (
                Guid groupId,
                Guid memberUserId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var groupBeforeRemoval = await client.GetGroupAsync(groupId, cancellationToken);
                await client.RemoveGroupMemberAsync(groupId, memberUserId, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    groupBeforeRemoval.Members
                        .Select(member => member.UserId)
                        .Where(id => id != actorUserId),
                    NotificationType.GroupMemberRemoved,
                    "Participante removido",
                    $"A composição do grupo {groupBeforeRemoval.Name} foi alterada.",
                    "/social",
                    cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent);

        groups.MapDelete("/{groupId:guid}", async (
                Guid groupId,
                HttpContext context,
                IDebtServiceClient client,
                NotificationService notifications,
                CancellationToken cancellationToken) =>
            {
                var groupBeforeDeletion = await client.GetGroupAsync(groupId, cancellationToken);
                await client.DeleteGroupAsync(groupId, cancellationToken);
                var actorUserId = AuthenticatedUser.GetId(context.User);
                await notifications.PublishAsync(
                    groupBeforeDeletion.Members
                        .Select(member => member.UserId)
                        .Where(id => id != actorUserId),
                    NotificationType.GroupDeleted,
                    "Grupo excluído",
                    $"O grupo {groupBeforeDeletion.Name} foi excluído.",
                    "/social",
                    cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent);
    }

    private static UserSnapshotRequest ToSnapshot(ApplicationUser user) =>
        new(user.Id, user.DisplayName, user.Email!);
}
