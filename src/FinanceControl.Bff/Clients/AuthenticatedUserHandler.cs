using System.IdentityModel.Tokens.Jwt;

namespace FinanceControl.Bff.Clients;

public sealed class AuthenticatedUserHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string UserIdHeaderName = "X-Finance-Control-User-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new InvalidOperationException("The authenticated request does not contain a valid user identifier.");
        }

        request.Headers.Remove(UserIdHeaderName);
        request.Headers.TryAddWithoutValidation(UserIdHeaderName, parsedUserId.ToString());
        return base.SendAsync(request, cancellationToken);
    }
}
