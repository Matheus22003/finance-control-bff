using System.Collections.Concurrent;
using FinanceControl.Bff.Email;

namespace FinanceControl.Bff.Tests;

public sealed class TestEmailSender : IApplicationEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _messages = new();

    public IReadOnlyList<SentEmail> Messages => _messages.ToArray();

    public Task SendAsync(
        string recipientAddress,
        string recipientName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        _messages.Enqueue(new SentEmail(recipientAddress, recipientName, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(
    string RecipientAddress,
    string RecipientName,
    string Subject,
    string HtmlBody);
