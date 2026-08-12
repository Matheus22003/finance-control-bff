using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Clients.Finance;

public sealed class FinanceServiceOptions
{
    public const string SectionName = "FinanceService";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 5;
}
