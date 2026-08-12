using FinanceControl.Bff.Clients.Finance;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.Bff.Errors;

internal sealed class FinanceServiceExceptionHandler(
    ILogger<FinanceServiceExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not FinanceServiceException financeException)
        {
            return false;
        }

        var (statusCode, title, detail) = financeException.Failure switch
        {
            FinanceServiceFailure.Timeout => (
                StatusCodes.Status504GatewayTimeout,
                "Finance Service timeout",
                "Finance Service did not respond within the configured timeout."),
            FinanceServiceFailure.Unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "Finance Service unavailable",
                "Finance Service is temporarily unavailable."),
            FinanceServiceFailure.Rejected when financeException.UpstreamStatusCode is
                StatusCodes.Status400BadRequest or
                StatusCodes.Status404NotFound or
                StatusCodes.Status409Conflict or
                StatusCodes.Status422UnprocessableEntity => (
                financeException.UpstreamStatusCode.Value,
                financeException.UpstreamProblem?.Title ?? "Finance request rejected",
                financeException.UpstreamProblem?.Detail ??
                "Finance Service rejected the request."),
            _ => (
                StatusCodes.Status502BadGateway,
                "Invalid Finance Service response",
                "Finance Service returned an invalid response.")
        };

        if (financeException.Failure == FinanceServiceFailure.Rejected)
        {
            logger.LogWarning(
                "Finance Service rejected the request with upstream status {UpstreamStatusCode}",
                financeException.UpstreamStatusCode);
        }
        else
        {
            logger.LogError(
                financeException,
                "Finance Service request failed with failure {Failure} and upstream status {UpstreamStatusCode}",
                financeException.Failure,
                financeException.UpstreamStatusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (financeException.Failure == FinanceServiceFailure.Rejected &&
            financeException.UpstreamProblem is not null)
        {
            foreach (var extension in financeException.UpstreamProblem.Extensions)
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
