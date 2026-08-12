using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Users;
using FinanceControl.Bff.Email;
using FinanceControl.Bff.Notifications;
using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Endpoints;

public static class UserEndpoints
{
    private const long MaxAvatarBytes = 1_048_576;
    private static readonly HashSet<string> SupportedAvatarTypes =
        ["image/jpeg", "image/png", "image/webp"];

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users").WithTags("Users").RequireAuthorization();

        users.MapGet("/me", GetCurrentProfile)
            .Produces<UserProfileResponse>();

        users.MapPut("/me/profile", UpdateProfile)
            .Produces<UserProfileResponse>()
            .ProducesValidationProblem();

        users.MapPut("/me/preferences", UpdatePreferences)
            .Produces<UserProfileResponse>()
            .ProducesValidationProblem();

        users.MapPost("/me/email-change", RequestEmailChange)
            .RequireRateLimiting("auth-sensitive")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapGet("/me/avatar", GetAvatar)
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPut("/me/avatar", UpdateAvatar)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UserProfileResponse>()
            .ProducesValidationProblem();

        users.MapDelete("/me/avatar", DeleteAvatar)
            .Produces(StatusCodes.Status204NoContent);

        users.MapPost("/me/export", ExportAccount)
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        users.MapGet("/me/deletion-eligibility", GetDeletionEligibility)
            .Produces<FinanceControl.Bff.Contracts.Users.AccountDeletionEligibilityResponse>();

        users.MapDelete("/me", DeleteAccount)
            .RequireRateLimiting("auth-sensitive")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapGet("/search", SearchUser)
            .Produces<UserDirectoryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetCurrentProfile(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var user = await FindCurrentAsync(context.User, userManager);
        return Results.Ok(ToProfileResponse(user));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IDebtServiceClient debtServiceClient,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDisplayName(request.DisplayName);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await FindCurrentAsync(context.User, userManager);
        user.DisplayName = request.DisplayName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityFailure(result);

        await debtServiceClient.UpdateUserSnapshotAsync(
            new UserSnapshotRequest(user.Id, user.DisplayName, user.Email!),
            cancellationToken);
        return Results.Ok(ToProfileResponse(user));
    }

    private static async Task<IResult> UpdatePreferences(
        UpdatePreferencesRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        if (!Enum.TryParse<ThemePreference>(request.Theme, true, out var theme))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["theme"] = ["Theme must be system, light, or dark."]
            });
        }

        var user = await FindCurrentAsync(context.User, userManager);
        user.ThemePreference = theme;
        user.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        user.PushNotificationsEnabled = request.PushNotificationsEnabled;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? Results.Ok(ToProfileResponse(user))
            : IdentityFailure(result);
    }

    private static async Task<IResult> RequestEmailChange(
        RequestEmailChangeRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IApplicationEmailSender emailSender,
        EmailLinkFactory linkFactory,
        CancellationToken cancellationToken)
    {
        var errors = ValidateEmailChange(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await FindCurrentAsync(context.User, userManager);
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password", detail: "The current password is incorrect.");
        }

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Email unchanged", detail: "The new email must be different from the current email.");
        }

        if (await userManager.FindByEmailAsync(newEmail) is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Email already registered",
                detail: "Another account already uses the requested email address.");
        }

        var rawToken = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var token = EmailTokenCodec.Encode(rawToken);
        var link = linkFactory.CreateEmailChangeLink(user.Id, newEmail, token);
        await emailSender.SendAsync(
            newEmail,
            user.DisplayName,
            "Confirme seu novo e-mail | Finance Control",
            AuthEmailTemplates.EmailChange(user.DisplayName, newEmail, link),
            cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> GetAvatar(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var user = await FindCurrentAsync(context.User, userManager);
        if (user.AvatarData is null || user.AvatarContentType is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Avatar not found", detail: "The account does not have a custom avatar.");
        }

        return Results.File(
            user.AvatarData,
            user.AvatarContentType,
            lastModified: user.AvatarUpdatedAt,
            enableRangeProcessing: false);
    }

    private static async Task<IResult> UpdateAvatar(
        IFormFile file,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = ValidateAvatar(file);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);

        var user = await FindCurrentAsync(context.User, userManager);
        user.AvatarData = stream.ToArray();
        user.AvatarContentType = file.ContentType.ToLowerInvariant();
        user.AvatarUpdatedAt = timeProvider.GetUtcNow();
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? Results.Ok(ToProfileResponse(user))
            : IdentityFailure(result);
    }

    private static async Task<IResult> DeleteAvatar(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var user = await FindCurrentAsync(context.User, userManager);
        user.AvatarData = null;
        user.AvatarContentType = null;
        user.AvatarUpdatedAt = null;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Results.NoContent() : IdentityFailure(result);
    }

    private static async Task<IResult> ExportAccount(
        PasswordVerificationRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IFinanceServiceClient financeServiceClient,
        IDebtServiceClient debtServiceClient,
        BffDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["Password is required."]
            });
        }

        var user = await FindCurrentAsync(context.User, userManager);
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password", detail: "The current password is incorrect.");
        }

        var incomesTask = financeServiceClient.GetIncomesAsync(cancellationToken);
        var expensesTask = financeServiceClient.GetExpensesAsync(cancellationToken);
        var peopleTask = debtServiceClient.GetPeopleAsync(cancellationToken);
        var debtsTask = debtServiceClient.GetDebtsAsync(cancellationToken);
        var friendsTask = debtServiceClient.GetFriendsAsync(cancellationToken);
        var incomingTask = debtServiceClient.GetIncomingFriendRequestsAsync(cancellationToken);
        var outgoingTask = debtServiceClient.GetOutgoingFriendRequestsAsync(cancellationToken);
        var groupsTask = debtServiceClient.GetGroupsAsync(cancellationToken);
        var notifications = await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == user.Id)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Type.ToString(),
                notification.Title,
                notification.Message,
                notification.Route,
                notification.IsRead,
                notification.ReadAt,
                notification.CreatedAt))
            .ToListAsync(cancellationToken);
        var sessions = await dbContext.UserSessions.AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new AccountSessionExport(
                session.Id,
                session.DeviceName,
                session.IpAddress,
                session.CreatedAt,
                session.LastUsedAt,
                session.ExpiresAt,
                session.RevokedAt))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(
            incomesTask,
            expensesTask,
            peopleTask,
            debtsTask,
            friendsTask,
            incomingTask,
            outgoingTask,
            groupsTask);

        var debts = await debtsTask;
        var debtExports = await Task.WhenAll(debts.Select(async debt => new DebtExportResponse(
            debt,
            await debtServiceClient.GetPaymentsAsync(debt.Id, cancellationToken),
            await debtServiceClient.GetHistoryAsync(debt.Id, cancellationToken))));

        var export = new AccountExportResponse(
            timeProvider.GetUtcNow(),
            new AccountProfileExport(
                user.Id,
                user.DisplayName,
                user.Email!,
                user.EmailConfirmed,
                user.AvatarData is not null,
                ToPreferencesResponse(user)),
            await incomesTask,
            await expensesTask,
            await peopleTask,
            debtExports,
            await friendsTask,
            await incomingTask,
            await outgoingTask,
            await groupsTask,
            notifications,
            sessions);

        var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });
        var fileName = $"finance-control-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return Results.File(json, "application/json", fileName);
    }

    private static async Task<IResult> GetDeletionEligibility(
        IDebtServiceClient debtServiceClient,
        CancellationToken cancellationToken)
    {
        var eligibility = await debtServiceClient.GetAccountDeletionEligibilityAsync(cancellationToken);
        return Results.Ok(ToDeletionEligibilityResponse(eligibility));
    }

    private static async Task<IResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IDebtServiceClient debtServiceClient,
        IFinanceServiceClient financeServiceClient,
        IOptions<AuthSessionOptions> sessionOptions,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = ["Password is required."];
        if (!string.Equals(request.Confirmation?.Trim(), "EXCLUIR", StringComparison.Ordinal))
            errors["confirmation"] = ["Type EXCLUIR to confirm permanent account deletion."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await FindCurrentAsync(context.User, userManager);
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password", detail: "The current password is incorrect.");
        }

        var eligibility = await debtServiceClient.GetAccountDeletionEligibilityAsync(cancellationToken);
        if (!eligibility.CanDelete)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account deletion blocked",
                detail: "Resolve the listed shared debt items before deleting the account.",
                extensions: new Dictionary<string, object?>
                {
                    ["blockers"] = eligibility.Blockers,
                    ["openDebtsCount"] = eligibility.OpenDebtsCount,
                    ["pendingPaymentsCount"] = eligibility.PendingPaymentsCount,
                    ["activeSettlementPlansCount"] = eligibility.ActiveSettlementPlansCount,
                    ["ownedGroupsCount"] = eligibility.OwnedGroupsCount
                });
        }

        await debtServiceClient.DeleteAccountDataAsync(cancellationToken);
        await financeServiceClient.DeleteAccountDataAsync(cancellationToken);
        var deletion = await userManager.DeleteAsync(user);
        if (!deletion.Succeeded) return IdentityFailure(deletion);

        context.Response.Cookies.Delete(sessionOptions.Value.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth"
        });
        return Results.NoContent();
    }

    private static async Task<IResult> SearchUser(
        string email,
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Email is required."]
            });
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.Id == AuthenticatedUser.GetId(context.User))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "No other Finance Control user was found with this email.");
        }

        return Results.Ok(ToDirectoryResponse(user));
    }

    internal static async Task<ApplicationUser> FindCurrentAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(AuthenticatedUser.GetId(principal).ToString());
        return user ?? throw new InvalidOperationException("The authenticated account no longer exists.");
    }

    internal static UserDirectoryResponse ToDirectoryResponse(ApplicationUser user) =>
        new(user.Id, user.DisplayName, user.Email!);

    internal static UserProfileResponse ToProfileResponse(ApplicationUser user) => new(
        user.Id,
        user.DisplayName,
        user.Email!,
        user.EmailConfirmed,
        user.AvatarData is null
            ? null
            : $"/api/v1/users/me/avatar?v={user.AvatarUpdatedAt?.ToUnixTimeSeconds() ?? 0}",
        ToPreferencesResponse(user));

    private static FinanceControl.Bff.Contracts.Users.AccountDeletionEligibilityResponse
        ToDeletionEligibilityResponse(Clients.Debt.AccountDeletionEligibilityResponse eligibility) => new(
            eligibility.CanDelete,
            eligibility.OpenDebtsCount,
            eligibility.PendingPaymentsCount,
            eligibility.ActiveSettlementPlansCount,
            eligibility.OwnedGroupsCount,
            eligibility.Blockers);

    private static UserPreferencesResponse ToPreferencesResponse(ApplicationUser user) => new(
        user.ThemePreference.ToString().ToLowerInvariant(),
        user.EmailNotificationsEnabled,
        user.PushNotificationsEnabled);

    private static Dictionary<string, string[]> ValidateDisplayName(string displayName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(displayName)) errors["displayName"] = ["Display name is required."];
        else if (displayName.Trim().Length > 120)
            errors["displayName"] = ["Display name must contain at most 120 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateEmailChange(RequestEmailChangeRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.NewEmail)) errors["newEmail"] = ["New email is required."];
        else if (!new EmailAddressAttribute().IsValid(request.NewEmail))
            errors["newEmail"] = ["New email must be valid."];
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = ["Password is required."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateAvatar(IFormFile file)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (file.Length == 0) errors["file"] = ["Avatar file is required."];
        else if (file.Length > MaxAvatarBytes) errors["file"] = ["Avatar must be at most 1 MB."];
        if (!SupportedAvatarTypes.Contains(file.ContentType.ToLowerInvariant()))
            errors["file"] = ["Avatar must be a JPEG, PNG, or WebP image."];
        return errors;
    }

    private static IResult IdentityFailure(IdentityResult result) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["account"] = result.Errors.Select(error => error.Description).ToArray()
        });
}
