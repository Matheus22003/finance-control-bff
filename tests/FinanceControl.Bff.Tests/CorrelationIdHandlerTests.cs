using System.Net;
using FinanceControl.Bff.Clients;
using FinanceControl.Bff.Observability;
using Microsoft.AspNetCore.Http;

namespace FinanceControl.Bff.Tests;

public sealed class CorrelationIdHandlerTests
{
    [Fact]
    public async Task SendAsync_PropagatesCorrelationIdToUpstreamService()
    {
        var correlationId = Guid.Parse("f9e2714e-a657-4c6c-bc2c-b6834fc64664").ToString("D");
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemName] = correlationId;

        var terminalHandler = new RecordingHandler();
        var correlationHandler = new CorrelationIdHandler(new HttpContextAccessor
        {
            HttpContext = context
        })
        {
            InnerHandler = terminalHandler
        };
        using var client = new HttpClient(correlationHandler);

        var response = await client.GetAsync("http://finance-service.test/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            correlationId,
            terminalHandler.Request?.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
