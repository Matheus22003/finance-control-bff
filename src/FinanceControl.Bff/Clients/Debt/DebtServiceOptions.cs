using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Clients.Debt;

public sealed class DebtServiceOptions
{
    public const string SectionName = "DebtService";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 5;
}
