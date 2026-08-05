namespace FinanceControl.Bff.Contracts.Auth;

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
