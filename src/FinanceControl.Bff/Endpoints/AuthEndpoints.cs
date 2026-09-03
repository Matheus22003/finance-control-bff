using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Contracts.Auth;
using FinanceControl.Bff.Email;
using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Endpoints;

public static class AuthEndpoints
{
    private const string CookiePath = "/api/v1/auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/register", Register).AllowAnonymous().RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces<RegistrationResponse>(202).ProducesValidationProblem().ProducesProblem(409);
        group.MapPost("/auth/login", Login).AllowAnonymous().RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces<LoginResponse>().ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403);
        group.MapPost("/auth/mobile/login", MobileLogin).AllowAnonymous().RequireRateLimiting("auth-sensitive").WithTags("Mobile Auth")
            .Produces<MobileSessionResponse>().ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403);
        group.MapPost("/auth/mobile/refresh", MobileRefresh).AllowAnonymous().RequireRateLimiting("auth-refresh").WithTags("Mobile Auth")
            .Produces<MobileSessionResponse>().ProducesValidationProblem().ProducesProblem(401);
        group.MapPost("/auth/mobile/logout", MobileLogout).AllowAnonymous().RequireRateLimiting("auth-refresh").WithTags("Mobile Auth")
            .Produces(204).ProducesValidationProblem();
        group.MapPost("/auth/refresh", Refresh).AllowAnonymous().RequireRateLimiting("auth-refresh").WithTags("Auth")
            .Produces<LoginResponse>().ProducesProblem(401);
        group.MapPost("/auth/confirm-email", ConfirmEmail).AllowAnonymous()
            .RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces(204).ProducesValidationProblem().ProducesProblem(400);
        group.MapPost("/auth/confirm-email-change", ConfirmEmailChange).AllowAnonymous()
            .RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces(204).ProducesValidationProblem().ProducesProblem(400).ProducesProblem(409);
        group.MapPost("/auth/resend-confirmation", ResendConfirmation).AllowAnonymous()
            .RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces(202).ProducesValidationProblem();
        group.MapPost("/auth/forgot-password", ForgotPassword).AllowAnonymous()
            .RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces(202).ProducesValidationProblem();
        group.MapPost("/auth/reset-password", ResetPassword).AllowAnonymous()
            .RequireRateLimiting("auth-sensitive").WithTags("Auth")
            .Produces(204).ProducesValidationProblem().ProducesProblem(400);
        group.MapPost("/auth/change-password", ChangePassword).WithTags("Auth")
            .Produces(204).ProducesValidationProblem().ProducesProblem(400);
        group.MapPost("/auth/logout", Logout).WithTags("Auth").Produces(204);
        group.MapGet("/auth/sessions", ListSessions).WithTags("Auth")
            .Produces<IReadOnlyList<SessionResponse>>();
        group.MapDelete("/auth/sessions/{sessionId:guid}", RevokeSession).WithTags("Auth")
            .Produces(204).ProducesProblem(404);
        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IApplicationEmailSender emailSender,
        EmailLinkFactory linkFactory,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Results.Problem(statusCode: 409, title: "Email already registered",
                detail: "An account already exists for the supplied email address.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = false
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        await SendConfirmationEmailAsync(user, userManager, emailSender, linkFactory, cancellationToken);
        return Results.Accepted(null, new RegistrationResponse(
            email,
            "Account created. Check your email to confirm the address before signing in."));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        BffDbContext dbContext,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        var errors = ValidateLogin(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Problem(statusCode: 401, title: "Invalid credentials",
                detail: "The supplied email or password is invalid.");
        }

        if (!user.EmailConfirmed)
        {
            return Results.Problem(statusCode: 403, title: "Email not confirmed",
                detail: "Confirm your email address before signing in.");
        }

        return await CreateSessionResponse(context, user, dbContext, tokenService,
            refreshTokenService, options.Value, timeProvider.GetUtcNow());
    }

    private static async Task<IResult> MobileLogin(
        MobileLoginRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        BffDbContext dbContext,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        var errors = ValidateMobileLogin(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Results.Problem(statusCode: 401, title: "Invalid credentials",
                detail: "The supplied email or password is invalid.");
        if (!user.EmailConfirmed)
            return Results.Problem(statusCode: 403, title: "Email not confirmed",
                detail: "Confirm your email address before signing in.");

        var now = timeProvider.GetUtcNow();
        var refresh = refreshTokenService.Create();
        var session = BuildSession(context, user, refresh, options.Value, now, Guid.NewGuid(),
            request.DeviceInstallationId.Trim(), FormatMobileDeviceName(request));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();
        return Results.Ok(CreateMobileSessionResponse(tokenService, user, session, refresh.Value));
    }

    private static async Task<IResult> MobileRefresh(
        MobileRefreshRequest request,
        HttpContext context,
        BffDbContext dbContext,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        var errors = ValidateMobileRefresh(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var previous = await dbContext.UserSessions.Include(session => session.User)
            .SingleOrDefaultAsync(session => session.RefreshTokenHash == refreshTokenService.Hash(request.RefreshToken));
        var now = timeProvider.GetUtcNow();
        if (previous is null || !string.Equals(previous.DeviceInstallationId, request.DeviceInstallationId.Trim(), StringComparison.Ordinal))
            return InvalidRefreshToken();
        if (!previous.User.EmailConfirmed || previous.ExpiresAt <= now)
        {
            previous.RevokedAt = now;
            await dbContext.SaveChangesAsync();
            return InvalidRefreshToken();
        }
        if (previous.RevokedAt is not null)
        {
            await RevokeSessionFamilyAsync(previous.FamilyId, dbContext, now);
            return InvalidRefreshToken();
        }

        var refresh = refreshTokenService.Create();
        var next = BuildSession(context, previous.User, refresh, options.Value, now, previous.FamilyId,
            previous.DeviceInstallationId, previous.DeviceName);
        previous.RevokedAt = now;
        previous.LastUsedAt = now;
        previous.ReplacedBySessionId = next.Id;
        dbContext.UserSessions.Add(next);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return InvalidRefreshToken();
        }

        return Results.Ok(CreateMobileSessionResponse(tokenService, previous.User, next, refresh.Value));
    }

    private static async Task<IResult> MobileLogout(
        MobileRefreshRequest request,
        BffDbContext dbContext,
        RefreshTokenService refreshTokenService,
        TimeProvider timeProvider)
    {
        var errors = ValidateMobileRefresh(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var session = await dbContext.UserSessions.SingleOrDefaultAsync(candidate =>
            candidate.RefreshTokenHash == refreshTokenService.Hash(request.RefreshToken) &&
            candidate.DeviceInstallationId == request.DeviceInstallationId.Trim());
        if (session is not null)
        {
            await RevokeSessionFamilyAsync(session.FamilyId, dbContext, timeProvider.GetUtcNow());
        }
        return Results.NoContent();
    }

    private static async Task<IResult> Refresh(
        HttpContext context,
        BffDbContext dbContext,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        if (!context.Request.Cookies.TryGetValue(options.Value.CookieName, out var rawToken) ||
            string.IsNullOrWhiteSpace(rawToken))
            return InvalidRefreshToken();

        var hash = refreshTokenService.Hash(rawToken);
        var previous = await dbContext.UserSessions.Include(session => session.User)
            .SingleOrDefaultAsync(session => session.RefreshTokenHash == hash);
        var now = timeProvider.GetUtcNow();
        if (previous is null) return InvalidRefreshToken();

        if (!previous.User.EmailConfirmed)
        {
            previous.RevokedAt = now;
            await dbContext.SaveChangesAsync();
            DeleteRefreshCookie(context, options.Value);
            return InvalidRefreshToken();
        }

        if (previous.RevokedAt is not null)
        {
            var activeFamilySessions = await dbContext.UserSessions
                .Where(session => session.FamilyId == previous.FamilyId && session.RevokedAt == null)
                .ToListAsync();
            foreach (var activeSession in activeFamilySessions) activeSession.RevokedAt = now;
            await dbContext.SaveChangesAsync();
            DeleteRefreshCookie(context, options.Value);
            return InvalidRefreshToken();
        }

        if (previous.ExpiresAt <= now)
        {
            previous.RevokedAt = now;
            await dbContext.SaveChangesAsync();
            DeleteRefreshCookie(context, options.Value);
            return InvalidRefreshToken();
        }

        var refresh = refreshTokenService.Create();
        var next = BuildSession(context, previous.User, refresh, options.Value, now, previous.FamilyId);
        previous.RevokedAt = now;
        previous.LastUsedAt = now;
        previous.ReplacedBySessionId = next.Id;
        dbContext.UserSessions.Add(next);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            DeleteRefreshCookie(context, options.Value);
            return InvalidRefreshToken();
        }
        WriteRefreshCookie(context, options.Value, refresh.Value, next.ExpiresAt);
        return Results.Ok(CreateLoginResponse(tokenService, previous.User, next.Id));
    }

    private static async Task<IResult> ConfirmEmail(
        ConfirmEmailRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var errors = ValidateTokenRequest(request.UserId, request.Token);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !EmailTokenCodec.TryDecode(request.Token, out var token))
            return InvalidEmailToken();

        if (user.EmailConfirmed) return Results.NoContent();

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Results.NoContent() : InvalidEmailToken();
    }

    private static async Task<IResult> ResendConfirmation(
        EmailRequest request,
        UserManager<ApplicationUser> userManager,
        IApplicationEmailSender emailSender,
        EmailLinkFactory linkFactory,
        CancellationToken cancellationToken)
    {
        var errors = ValidateEmail(request.Email);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            await SendConfirmationEmailAsync(user, userManager, emailSender, linkFactory, cancellationToken);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        BffDbContext dbContext,
        IDebtServiceClient debtServiceClient,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = ValidateTokenRequest(request.UserId, request.Token);
        foreach (var error in ValidateEmail(request.NewEmail)) errors[error.Key] = error.Value;
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !EmailTokenCodec.TryDecode(request.Token, out var token))
            return InvalidEmailToken();

        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null && existingUser.Id != user.Id)
        {
            return Results.Problem(statusCode: 409, title: "Email already registered",
                detail: "Another account already uses the requested email address.");
        }

        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.ChangeEmailAsync(user, normalizedEmail, token);
            if (!emailResult.Succeeded) return InvalidEmailToken();
        }

        if (!string.Equals(user.UserName, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var userNameResult = await userManager.SetUserNameAsync(user, normalizedEmail);
            if (!userNameResult.Succeeded) return IdentityFailure(userNameResult);
        }

        await debtServiceClient.UpdateUserSnapshotAsync(
            new UserSnapshotRequest(user.Id, user.DisplayName, user.Email!),
            cancellationToken);

        await RevokeAllSessionsAsync(user.Id, dbContext, timeProvider.GetUtcNow());
        DeleteRefreshCookie(context, options.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPassword(
        EmailRequest request,
        UserManager<ApplicationUser> userManager,
        IApplicationEmailSender emailSender,
        EmailLinkFactory linkFactory,
        CancellationToken cancellationToken)
    {
        var errors = ValidateEmail(request.Email);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.EmailConfirmed)
        {
            var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var token = EmailTokenCodec.Encode(rawToken);
            var link = linkFactory.CreatePasswordResetLink(user.Id, token);
            await emailSender.SendAsync(
                user.Email!,
                user.DisplayName,
                "Redefina sua senha | Finance Control",
                AuthEmailTemplates.PasswordReset(user.DisplayName, link),
                cancellationToken);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        BffDbContext dbContext,
        TimeProvider timeProvider)
    {
        var errors = ValidatePasswordTokenRequest(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !EmailTokenCodec.TryDecode(request.Token, out var token))
            return InvalidEmailToken();

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        await RevokeAllSessionsAsync(user.Id, dbContext, timeProvider.GetUtcNow());
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        BffDbContext dbContext,
        IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        var errors = ValidatePasswordChange(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await userManager.FindByIdAsync(GetUserId(context.User).ToString());
        if (user is null)
            return Results.Problem(statusCode: 400, title: "Unable to change password",
                detail: "The authenticated account could not be found.");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        await RevokeAllSessionsAsync(user.Id, dbContext, timeProvider.GetUtcNow());
        DeleteRefreshCookie(context, options.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext context, BffDbContext dbContext,
        RefreshTokenService refreshTokenService, IOptions<AuthSessionOptions> options,
        TimeProvider timeProvider)
    {
        if (context.Request.Cookies.TryGetValue(options.Value.CookieName, out var rawToken))
        {
            var hash = refreshTokenService.Hash(rawToken);
            var userId = GetUserId(context.User);
            var session = await dbContext.UserSessions.SingleOrDefaultAsync(candidate =>
                candidate.UserId == userId && candidate.RefreshTokenHash == hash);
            if (session is not null && session.RevokedAt is null)
            {
                session.RevokedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync();
            }
        }
        DeleteRefreshCookie(context, options.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> ListSessions(HttpContext context, BffDbContext dbContext,
        TimeProvider timeProvider)
    {
        var userId = GetUserId(context.User);
        var currentId = GetSessionId(context.User);
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.UserSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > now)
            .OrderByDescending(session => session.LastUsedAt)
            .Select(session => new SessionResponse(session.Id, session.DeviceName, session.IpAddress,
                session.CreatedAt, session.LastUsedAt, session.ExpiresAt, session.Id == currentId))
            .ToListAsync();
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeSession(Guid sessionId, HttpContext context,
        BffDbContext dbContext, TimeProvider timeProvider)
    {
        var userId = GetUserId(context.User);
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(candidate =>
            candidate.Id == sessionId && candidate.UserId == userId && candidate.RevokedAt == null);
        if (session is null)
            return Results.Problem(statusCode: 404, title: "Session not found",
                detail: "The requested active session does not exist.");
        session.RevokedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> CreateSessionResponse(HttpContext context, ApplicationUser user,
        BffDbContext dbContext, TokenService tokenService, RefreshTokenService refreshTokenService,
        AuthSessionOptions options, DateTimeOffset now)
    {
        var refresh = refreshTokenService.Create();
        var session = BuildSession(context, user, refresh, options, now, Guid.NewGuid());
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();
        WriteRefreshCookie(context, options, refresh.Value, session.ExpiresAt);
        return Results.Ok(CreateLoginResponse(tokenService, user, session.Id));
    }

    private static UserSession BuildSession(HttpContext context, ApplicationUser user,
        RefreshToken refresh, AuthSessionOptions options, DateTimeOffset now, Guid familyId,
        string? deviceInstallationId = null, string? deviceName = null) => new()
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = user.Id,
            User = user,
            RefreshTokenHash = refresh.Hash,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(options.RefreshTokenDays),
            DeviceName = deviceName ?? GetDeviceName(context.Request.Headers.UserAgent.ToString()),
            DeviceInstallationId = deviceInstallationId,
            IpAddress = Truncate(context.Connection.RemoteIpAddress?.ToString(), 64, null)
        };

    private static LoginResponse CreateLoginResponse(TokenService tokenService, ApplicationUser user, Guid sessionId)
    {
        var access = tokenService.CreateToken(user.Id, user.Email!, sessionId);
        return new LoginResponse(access.Value, "Bearer", access.ExpiresAt,
            new AuthUserResponse(user.Id, user.Email!, user.DisplayName));
    }

    private static MobileSessionResponse CreateMobileSessionResponse(TokenService tokenService,
        ApplicationUser user, UserSession session, string refreshToken)
    {
        var access = tokenService.CreateToken(user.Id, user.Email!, session.Id);
        return new MobileSessionResponse(access.Value, refreshToken, "Bearer", access.ExpiresAt,
            session.DeviceInstallationId!, new AuthUserResponse(user.Id, user.Email!, user.DisplayName));
    }

    private static async Task RevokeSessionFamilyAsync(Guid familyId, BffDbContext dbContext,
        DateTimeOffset revokedAt)
    {
        var activeSessions = await dbContext.UserSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ToListAsync();
        foreach (var session in activeSessions) session.RevokedAt = revokedAt;
        await dbContext.SaveChangesAsync();
    }

    private static void WriteRefreshCookie(HttpContext context, AuthSessionOptions options,
        string value, DateTimeOffset expiresAt) => context.Response.Cookies.Append(options.CookieName, value,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expiresAt,
            IsEssential = true
        });

    private static void DeleteRefreshCookie(HttpContext context, AuthSessionOptions options) =>
        context.Response.Cookies.Delete(options.CookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = CookiePath
            });

    private static IResult InvalidRefreshToken() => Results.Problem(statusCode: 401,
        title: "Invalid session", detail: "The refresh session is missing, expired, revoked, or invalid.");

    private static IResult InvalidEmailToken() => Results.Problem(statusCode: 400,
        title: "Invalid or expired link",
        detail: "The supplied security link is invalid, has expired, or has already been used.");

    private static IResult IdentityFailure(IdentityResult result) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["password"] = result.Errors.Select(error => error.Description).ToArray()
        });

    private static async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IApplicationEmailSender emailSender,
        EmailLinkFactory linkFactory,
        CancellationToken cancellationToken)
    {
        var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var token = EmailTokenCodec.Encode(rawToken);
        var link = linkFactory.CreateConfirmationLink(user.Id, token);
        await emailSender.SendAsync(
            user.Email!,
            user.DisplayName,
            "Confirme seu e-mail | Finance Control",
            AuthEmailTemplates.Confirmation(user.DisplayName, link),
            cancellationToken);
    }

    private static async Task RevokeAllSessionsAsync(
        Guid userId,
        BffDbContext dbContext,
        DateTimeOffset revokedAt)
    {
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync();
        foreach (var session in sessions) session.RevokedAt = revokedAt;
        await dbContext.SaveChangesAsync();
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static Guid GetSessionId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sid)!);

    private static Dictionary<string, string[]> ValidateMobileLogin(MobileLoginRequest request)
    {
        var errors = ValidateLogin(new LoginRequest(request.Email, request.Password));
        if (!Guid.TryParse(request.DeviceInstallationId, out _))
            errors["deviceInstallationId"] = ["Device installation id must be a UUID."];
        if (string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Trim().Length > 120)
            errors["deviceName"] = ["Device name is required and must contain at most 120 characters."];
        if (!string.Equals(request.Platform, "android", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Platform, "ios", StringComparison.OrdinalIgnoreCase))
            errors["platform"] = ["Platform must be android or ios."];
        if (string.IsNullOrWhiteSpace(request.AppVersion) || request.AppVersion.Trim().Length > 40)
            errors["appVersion"] = ["App version is required and must contain at most 40 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateMobileRefresh(MobileRefreshRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) errors["refreshToken"] = ["Refresh token is required."];
        if (!Guid.TryParse(request.DeviceInstallationId, out _))
            errors["deviceInstallationId"] = ["Device installation id must be a UUID."];
        return errors;
    }

    private static string FormatMobileDeviceName(MobileLoginRequest request) =>
        $"{request.DeviceName.Trim()} · {request.Platform.Trim().ToLowerInvariant()} {request.AppVersion.Trim()}";

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = ["Email is required."];
        else if (!new EmailAddressAttribute().IsValid(request.Email)) errors["email"] = ["Email must be valid."];
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = ["Password is required."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = ValidateLogin(new LoginRequest(request.Email, request.Password));
        if (string.IsNullOrWhiteSpace(request.DisplayName)) errors["displayName"] = ["Display name is required."];
        else if (request.DisplayName.Trim().Length > 120) errors["displayName"] = ["Display name must contain at most 120 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateEmail(string email)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(email)) errors["email"] = ["Email is required."];
        else if (!new EmailAddressAttribute().IsValid(email)) errors["email"] = ["Email must be valid."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateTokenRequest(Guid userId, string token)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (userId == Guid.Empty) errors["userId"] = ["User id is required."];
        if (string.IsNullOrWhiteSpace(token)) errors["token"] = ["Token is required."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidatePasswordTokenRequest(ResetPasswordRequest request)
    {
        var errors = ValidateTokenRequest(request.UserId, request.Token);
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            errors["newPassword"] = ["New password is required."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidatePasswordChange(ChangePasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            errors["currentPassword"] = ["Current password is required."];
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            errors["newPassword"] = ["New password is required."];
        if (!string.IsNullOrWhiteSpace(request.CurrentPassword) &&
            string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            errors["newPassword"] = ["New password must be different from the current password."];
        return errors;
    }

    private static string? Truncate(string? value, int length, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Length <= length ? value : value[..length];
    }

    private static string GetDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Dispositivo desconhecido";

        var browser = userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Microsoft Edge"
            : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : "Navegador";
        var operatingSystem = userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
              userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : "dispositivo desconhecido";

        return $"{browser} no {operatingSystem}";
    }
}
