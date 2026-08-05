using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceControl.Bff.Auth;

public static class AuthenticatedUser
{
    public static Guid GetId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated user identifier is invalid.");
    }
}
