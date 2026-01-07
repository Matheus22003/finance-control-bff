using FinanceControl.Bff.Contracts.Auth;
using LoginRequest = Microsoft.AspNetCore.Identity.Data.LoginRequest;

namespace FinanceControl.Bff.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/auth/login", (LoginRequest request) =>
        {
            // Mock de autenticação (MVP)
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest("Invalid credentials");

            return Results.Ok(new LoginResponse()
            {
                Token = "mock-jwt-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
        });
    }
}