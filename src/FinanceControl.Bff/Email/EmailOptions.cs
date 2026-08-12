using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 1025;

    [Required, EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    [Required]
    public string FromName { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public SmtpSecurityMode Security { get; init; } = SmtpSecurityMode.None;

    [Required, Url]
    public string FrontendBaseUrl { get; init; } = string.Empty;
}

public enum SmtpSecurityMode
{
    None,
    StartTls,
    SslOnConnect
}
