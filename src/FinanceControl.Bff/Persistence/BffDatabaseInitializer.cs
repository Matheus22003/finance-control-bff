using FinanceControl.Bff.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Persistence;

public sealed class BffDatabaseInitializer(
    BffDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<DemoUserOptions> demoUserOptions)
{
    public static readonly Guid DemoUserId = Guid.Parse("7f805b46-0b56-4a5d-86eb-d4f53c92db93");
    public static readonly Guid FriendUserId = Guid.Parse("8750c27d-a3ff-4c8f-997b-c6f230005040");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var demoUser = demoUserOptions.Value;
        await EnsureUserAsync(DemoUserId, demoUser.Email, demoUser.Password, "Conta demo");
        if (!string.IsNullOrWhiteSpace(demoUser.FriendEmail) &&
            !string.IsNullOrWhiteSpace(demoUser.FriendPassword))
        {
            await EnsureUserAsync(
                FriendUserId,
                demoUser.FriendEmail,
                demoUser.FriendPassword,
                "Conta amiga");
        }
    }

    private async Task EnsureUserAsync(Guid id, string email, string password, string displayName)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException($"Could not create the demo user: {errors}");
        }
    }
}
