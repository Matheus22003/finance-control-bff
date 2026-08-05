using FinanceControl.Bff.Clients;

namespace FinanceControl.Bff.Clients.Finance;

public sealed class FinanceServiceException : Exception
{
    public FinanceServiceException(
        FinanceServiceFailure failure,
        string message,
        Exception? innerException = null,
        int? upstreamStatusCode = null,
        UpstreamProblemDetails? upstreamProblem = null) : base(message, innerException)
    {
        Failure = failure;
        UpstreamStatusCode = upstreamStatusCode;
        UpstreamProblem = upstreamProblem;
    }

    public FinanceServiceFailure Failure { get; }

    public int? UpstreamStatusCode { get; }

    public UpstreamProblemDetails? UpstreamProblem { get; }
}

public enum FinanceServiceFailure
{
    Unavailable,
    Timeout,
    InvalidResponse,
    Rejected
}
