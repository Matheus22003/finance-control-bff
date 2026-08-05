namespace FinanceControl.Bff.Contracts.Auth;

public sealed record RegisterRequest(string DisplayName, string Email, string Password);
