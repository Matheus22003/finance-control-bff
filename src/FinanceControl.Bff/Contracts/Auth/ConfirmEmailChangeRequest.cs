namespace FinanceControl.Bff.Contracts.Auth;

public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token);
