using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Notifications;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Persistence;

public sealed class BffDbContext(DbContextOptions<BffDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<UserPushSubscription> PushSubscriptions => Set<UserPushSubscription>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        builder.Entity<ApplicationUser>()
            .Property(user => user.DisplayName)
            .HasMaxLength(120)
            .IsRequired();
        builder.Entity<ApplicationUser>()
            .Property(user => user.ThemePreference)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Entity<ApplicationUser>()
            .Property(user => user.AvatarContentType)
            .HasMaxLength(40);

        builder.Entity<UserSession>(session =>
        {
            session.ToTable("user_sessions");
            session.HasKey(candidate => candidate.Id);
            session.Property(candidate => candidate.RefreshTokenHash).HasMaxLength(64).IsRequired();
            session.Property(candidate => candidate.DeviceName).HasMaxLength(200).IsRequired();
            session.Property(candidate => candidate.DeviceInstallationId).HasMaxLength(64);
            session.Property(candidate => candidate.IpAddress).HasMaxLength(64);
            session.Property(candidate => candidate.RevokedAt).IsConcurrencyToken();
            session.HasIndex(candidate => candidate.RefreshTokenHash).IsUnique();
            session.HasIndex(candidate => new { candidate.UserId, candidate.ExpiresAt });
            session.HasOne(candidate => candidate.User)
                .WithMany()
                .HasForeignKey(candidate => candidate.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserNotification>(notification =>
        {
            notification.ToTable("notifications");
            notification.HasKey(candidate => candidate.Id);
            notification.Property(candidate => candidate.Type)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            notification.Property(candidate => candidate.Title).HasMaxLength(120).IsRequired();
            notification.Property(candidate => candidate.Message).HasMaxLength(500).IsRequired();
            notification.Property(candidate => candidate.Route).HasMaxLength(200);
            notification.Property(candidate => candidate.DeduplicationKey).HasMaxLength(200);
            notification.HasIndex(candidate => new
            {
                candidate.UserId,
                candidate.IsRead,
                candidate.CreatedAt
            });
            notification.HasIndex(candidate => new
            {
                candidate.UserId,
                candidate.DeduplicationKey
            })
                .IsUnique()
                .HasFilter("\"DeduplicationKey\" IS NOT NULL");
            notification.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(candidate => candidate.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationPreference>(preference =>
        {
            preference.ToTable("notification_preferences");
            preference.HasKey(candidate => new { candidate.UserId, candidate.Type });
            preference.Property(candidate => candidate.Type)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            preference.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(candidate => candidate.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPushSubscription>(subscription =>
        {
            subscription.ToTable("push_subscriptions");
            subscription.HasKey(candidate => candidate.Id);
            subscription.Property(candidate => candidate.Endpoint).HasMaxLength(2048).IsRequired();
            subscription.Property(candidate => candidate.EndpointHash).HasMaxLength(64).IsRequired();
            subscription.Property(candidate => candidate.P256Dh).HasMaxLength(512).IsRequired();
            subscription.Property(candidate => candidate.Auth).HasMaxLength(512).IsRequired();
            subscription.Property(candidate => candidate.DeviceName).HasMaxLength(200).IsRequired();
            subscription.HasIndex(candidate => candidate.EndpointHash).IsUnique();
            subscription.HasIndex(candidate => new { candidate.UserId, candidate.UpdatedAt });
            subscription.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(candidate => candidate.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
