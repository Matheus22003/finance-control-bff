namespace FinanceControl.Bff.Contracts.Auth;

public record LoginRequest(
    string Email,
    string Password
);