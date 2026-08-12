using FinanceControl.Bff.Ai;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.Bff.Errors;

internal sealed class AiProviderExceptionHandler(
    ILogger<AiProviderExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AiProviderException providerException)
        {
            return false;
        }

        var (statusCode, title, detail) = providerException.Failure switch
        {
            AiProviderFailure.Timeout => (
                StatusCodes.Status504GatewayTimeout,
                "AI provider timeout",
                "The configured AI provider did not respond in time."),
            AiProviderFailure.Unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "AI provider unavailable",
                "The configured AI provider is temporarily unavailable or rate limited."),
            _ => (
                StatusCodes.Status502BadGateway,
                "Invalid AI provider response",
                "The configured AI provider rejected the request or returned an invalid response.")
        };

        logger.LogError(
            providerException,
            "AI provider failed with failure {Failure} and upstream status {UpstreamStatusCode}",
            providerException.Failure,
            providerException.UpstreamStatusCode);

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
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
