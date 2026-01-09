using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Contracts.Auth;
using LoginRequest = Microsoft.AspNetCore.Identity.Data.LoginRequest;

namespace FinanceControl.Bff.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/auth/login", (LoginRequest request, TokenService tokens) =>
            {
                // MVP: validação mock (substituir por user store futuramente)
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return Results.BadRequest(new { message = "Invalid credentials" });

                // MVP: qualquer email/senha "logam"
                var userId = Guid.NewGuid();

                var (token, expiresAt) = tokens.CreateToken(userId, request.Email);

                return Results.Ok(new LoginResponse(token, expiresAt));
            })
            .WithName("Login")
            .WithTags("Auth");
    }
}