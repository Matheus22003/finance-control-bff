namespace FinanceControl.Bff.Contracts.Auth;

public record LoginResponse(string Token, DateTime ExpiresAt);