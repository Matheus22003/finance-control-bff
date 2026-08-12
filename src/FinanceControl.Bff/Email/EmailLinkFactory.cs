using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Email;

public sealed class EmailLinkFactory(IOptions<EmailOptions> options)
{
    private readonly string _frontendBaseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');

    public string CreateConfirmationLink(Guid userId, string token) =>
        QueryHelpers.AddQueryString(
            $"{_frontendBaseUrl}/confirm-email",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = token
            });

    public string CreatePasswordResetLink(Guid userId, string token) =>
        QueryHelpers.AddQueryString(
            $"{_frontendBaseUrl}/reset-password",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = token
            });

    public string CreateEmailChangeLink(Guid userId, string newEmail, string token) =>
        QueryHelpers.AddQueryString(
            $"{_frontendBaseUrl}/confirm-email-change",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["newEmail"] = newEmail,
                ["token"] = token
            });
}
