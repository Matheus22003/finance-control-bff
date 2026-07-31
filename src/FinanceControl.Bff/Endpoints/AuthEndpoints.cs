using System.ComponentModel.DataAnnotations;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Contracts.Auth;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Endpoints;

public static class AuthEndpoints
{
    private static readonly Guid DemoUserId = Guid.Parse("7f805b46-0b56-4a5d-86eb-d4f53c92db93");

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/login", Login)
            .AllowAnonymous()
            .WithName("Login")
            .WithTags("Auth")
            .Accepts<LoginRequest>("application/json")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static IResult Login(
        LoginRequest request,
        TokenService tokenService,
        IOptions<DemoUserOptions> demoUserOptions)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var demoUser = demoUserOptions.Value;
        var credentialsAreValid =
            string.Equals(request.Email.Trim(), demoUser.Email, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.Password, demoUser.Password, StringComparison.Ordinal);

        if (!credentialsAreValid)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "The supplied email or password is invalid.");
        }

        var token = tokenService.CreateToken(DemoUserId, demoUser.Email);
        return Results.Ok(new LoginResponse(token.Value, "Bearer", token.ExpiresAt));
    }

    private static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            errors["email"] = ["Email must be valid."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors;
    }
}
