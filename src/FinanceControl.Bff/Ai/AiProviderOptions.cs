using System.ComponentModel.DataAnnotations;

namespace FinanceControl.Bff.Ai;

public sealed class AiProviderOptions
{
    public const string SectionName = "Ai";
    public const string MockProvider = "Mock";
    public const string OpenAiCompatibleProvider = "OpenAiCompatible";

    [Required]
    public string Provider { get; init; } = MockProvider;

    public string BaseUrl { get; init; } = "https://api.groq.com/openai/v1/";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "openai/gpt-oss-20b";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;

    [Range(128, 4_096)]
    public int MaxOutputTokens { get; init; } = 800;

    public bool UseJsonResponseFormat { get; init; }

    public string? ApplicationUrl { get; init; }

    public string? ApplicationName { get; init; } = "Finance Control";

    public bool UsesExternalProvider =>
        Provider.Equals(OpenAiCompatibleProvider, StringComparison.OrdinalIgnoreCase);
}
