namespace FinanceControl.Bff.Email;

public interface IApplicationEmailSender
{
    Task SendAsync(
        string recipientAddress,
        string recipientName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken);
}
