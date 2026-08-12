namespace FinanceControl.Bff.Contracts.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
