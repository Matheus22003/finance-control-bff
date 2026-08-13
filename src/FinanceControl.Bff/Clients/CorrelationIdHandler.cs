using FinanceControl.Bff.Observability;

namespace FinanceControl.Bff.Clients;

public sealed class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var correlationId = httpContext is null
            ? null
            : CorrelationIdMiddleware.GetCorrelationId(httpContext);

        request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
