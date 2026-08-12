using FinanceControl.Bff.Clients;

namespace FinanceControl.Bff.Clients.Debt;

public sealed class DebtServiceException : Exception
{
    public DebtServiceException(
        DebtServiceFailure failure,
        string message,
        Exception? innerException = null,
        int? upstreamStatusCode = null,
        UpstreamProblemDetails? upstreamProblem = null) : base(message, innerException)
    {
        Failure = failure;
        UpstreamStatusCode = upstreamStatusCode;
        UpstreamProblem = upstreamProblem;
    }

    public DebtServiceFailure Failure { get; }

    public int? UpstreamStatusCode { get; }

    public UpstreamProblemDetails? UpstreamProblem { get; }
}

public enum DebtServiceFailure
{
    Unavailable,
    Timeout,
    InvalidResponse,
    Rejected
}
