namespace FinanceControl.Bff.Clients.Debt;

public sealed record UserSnapshotRequest(Guid UserId, string DisplayName, string Email);

public sealed record CreateFriendRequest(
    Guid TargetUserId,
    string RequesterDisplayName,
    string RequesterEmail,
    string TargetDisplayName,
    string TargetEmail);

public sealed record FriendshipResponse(
    Guid Id,
    Guid RequesterUserId,
    string RequesterDisplayName,
    string RequesterEmail,
    Guid AddresseeUserId,
    string AddresseeDisplayName,
    string AddresseeEmail,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FriendResponse(
    Guid FriendshipId,
    Guid UserId,
    string DisplayName,
    string Email,
    DateTimeOffset FriendsSince);

public sealed record CreateGroupServiceRequest(
    string Name,
    string? Description,
    UserSnapshotRequest Owner,
    IReadOnlyList<UserSnapshotRequest> Members);

public sealed record UpdateGroupRequest(string Name, string? Description);

public sealed record AddGroupMemberServiceRequest(UserSnapshotRequest Member);

public sealed record GroupResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GroupMemberResponse> Members);

public sealed record GroupMemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record SendFriendRequest(string Email);

public sealed record CreateGroupRequest(
    string Name,
    string? Description,
    IReadOnlyList<Guid> MemberUserIds);

public sealed record AddGroupMemberRequest(Guid UserId);

public sealed record UserDirectoryResponse(Guid Id, string DisplayName, string Email);
