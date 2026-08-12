namespace FinanceControl.Bff.Ai;

public enum AiProviderFailure
{
    Unavailable,
    Timeout,
    Rejected,
    InvalidResponse
}

public sealed class AiProviderException(
    AiProviderFailure failure,
    string message,
    Exception? innerException = null,
    int? upstreamStatusCode = null) : Exception(message, innerException)
{
    public AiProviderFailure Failure { get; } = failure;

    public int? UpstreamStatusCode { get; } = upstreamStatusCode;
}
