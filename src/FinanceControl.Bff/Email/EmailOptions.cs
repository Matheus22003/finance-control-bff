using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public const string SmtpProvider = "Smtp";
    public const string BrevoProvider = "Brevo";

    [Required]
    public string Provider { get; init; } = SmtpProvider;

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
    public string ApiBaseUrl { get; init; } = "https://api.brevo.com/v3/";

    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;

    [Required, Url]
    public string FrontendBaseUrl { get; init; } = string.Empty;

    public bool UsesBrevo => string.Equals(Provider, BrevoProvider, StringComparison.OrdinalIgnoreCase);
}

public enum SmtpSecurityMode
{
    None,
    StartTls,
    SslOnConnect
}
