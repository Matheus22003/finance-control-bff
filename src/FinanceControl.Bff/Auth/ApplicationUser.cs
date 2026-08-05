using Microsoft.AspNetCore.Identity;

namespace FinanceControl.Bff.Auth;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool PushNotificationsEnabled { get; set; } = true;
    public byte[]? AvatarData { get; set; }
    public string? AvatarContentType { get; set; }
    public DateTimeOffset? AvatarUpdatedAt { get; set; }
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}
