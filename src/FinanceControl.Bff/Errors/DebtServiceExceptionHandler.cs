using FinanceControl.Bff.Clients.Debt;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.Bff.Errors;

internal sealed class DebtServiceExceptionHandler(
    ILogger<DebtServiceExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DebtServiceException debtException)
        {
            return false;
        }

        var (statusCode, title, detail) = debtException.Failure switch
        {
            DebtServiceFailure.Timeout => (
                StatusCodes.Status504GatewayTimeout,
                "Debt Service timeout",
                "Debt Service did not respond within the configured timeout."),
            DebtServiceFailure.Unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "Debt Service unavailable",
                "Debt Service is temporarily unavailable."),
            DebtServiceFailure.Rejected when debtException.UpstreamStatusCode is
                StatusCodes.Status400BadRequest or
                StatusCodes.Status404NotFound or
                StatusCodes.Status409Conflict or
                StatusCodes.Status422UnprocessableEntity => (
                debtException.UpstreamStatusCode.Value,
                debtException.UpstreamProblem?.Title ?? "Debt request rejected",
                debtException.UpstreamProblem?.Detail ??
                "Debt Service rejected the request."),
            _ => (
                StatusCodes.Status502BadGateway,
                "Invalid Debt Service response",
                "Debt Service returned an invalid response.")
        };

        if (debtException.Failure == DebtServiceFailure.Rejected)
        {
            logger.LogWarning(
                "Debt Service rejected the request with upstream status {UpstreamStatusCode}",
                debtException.UpstreamStatusCode);
        }
        else
        {
            logger.LogError(
                debtException,
                "Debt Service request failed with failure {Failure} and upstream status {UpstreamStatusCode}",
                debtException.Failure,
                debtException.UpstreamStatusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (debtException.Failure == DebtServiceFailure.Rejected &&
            debtException.UpstreamProblem is not null)
        {
            foreach (var extension in debtException.UpstreamProblem.Extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }
        }

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

        return true;
    }
}
