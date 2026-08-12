namespace FinanceControl.Bff.Contracts.Auth;

public sealed record ConfirmEmailRequest(Guid UserId, string Token);
