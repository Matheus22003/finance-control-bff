namespace FinanceControl.Bff.Contracts.Auth;

public record LoginResponse
{
    public string Token { get; init; }
    public DateTime ExpiresAt { get; init; }
}