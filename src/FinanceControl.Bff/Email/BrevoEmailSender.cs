using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace FinanceControl.Bff.Email;

public sealed class BrevoEmailSender(HttpClient httpClient, IOptions<EmailOptions> options)
    : IApplicationEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string recipientAddress,
        string recipientName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email");
        request.Headers.Add("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                email = _options.FromAddress,
                name = _options.FromName
            },
            to = new[]
            {
                new
                {
                    email = recipientAddress,
                    name = recipientName
                }
            },
            subject,
            htmlContent = htmlBody
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
