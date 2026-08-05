namespace FinanceControl.Bff.Contracts.Users;

public sealed record UserProfileResponse(
    Guid Id,
    string DisplayName,
    string Email,
    bool EmailConfirmed,
    string? AvatarUrl,
    UserPreferencesResponse Preferences);

public sealed record UserPreferencesResponse(
    string Theme,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled);

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record UpdatePreferencesRequest(
    string Theme,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled);

public sealed record RequestEmailChangeRequest(string NewEmail, string Password);

public sealed record PasswordVerificationRequest(string Password);
