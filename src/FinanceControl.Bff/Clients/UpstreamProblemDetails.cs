using System.Text.Json;

namespace FinanceControl.Bff.Clients;

public sealed record UpstreamProblemDetails(
    string? Title,
    string? Detail,
    IReadOnlyDictionary<string, JsonElement> Extensions);
