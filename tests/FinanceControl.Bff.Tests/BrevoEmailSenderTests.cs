using System.Net;
using System.Text.Json;
using FinanceControl.Bff.Email;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Tests;

public sealed class BrevoEmailSenderTests
{
    [Fact]
    public async Task SendAsyncPostsTransactionalEmailWithApiKey()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/")
        };
        var options = Options.Create(new EmailOptions
        {
            Provider = EmailOptions.BrevoProvider,
            FromAddress = "no-reply@example.com",
            FromName = "Finance Control",
            ApiKey = "test-api-key",
            FrontendBaseUrl = "https://finance-control.pages.dev"
        });
        var sender = new BrevoEmailSender(httpClient, options);

        await sender.SendAsync(
            "person@example.com",
            "Person",
            "Confirm your account",
            "<strong>Confirm</strong>",
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.RequestUri?.ToString());
        Assert.Equal("test-api-key", handler.ApiKey);

        using var payload = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal("no-reply@example.com", payload.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("person@example.com", payload.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Confirm your account", payload.RootElement.GetProperty("subject").GetString());
        Assert.Equal("<strong>Confirm</strong>", payload.RootElement.GetProperty("htmlContent").GetString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"messageId\":\"test\"}")
            };
        }
    }
}
