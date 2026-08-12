namespace FinanceControl.Bff.Contracts.Auth;

public sealed record AuthUserResponse(Guid Id, string Email, string DisplayName);
