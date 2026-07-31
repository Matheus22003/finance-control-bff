using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.Bff.Auth;

internal static class JwtProblemDetailsWriter
{
    public static async Task WriteAsync(HttpContext httpContext, int statusCode, string title, string detail)
    {
        httpContext.Response.StatusCode = statusCode;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            });
        }
    }
}
