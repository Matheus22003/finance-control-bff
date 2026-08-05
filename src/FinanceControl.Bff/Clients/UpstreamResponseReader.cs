using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceControl.Bff.Clients;

internal static class UpstreamResponseReader
{
    public static async Task<TResponse> ReadRequiredJsonAsync<TResponse>(
        HttpResponseMessage response,
        string serviceName,
        Func<string, Exception?, Exception> exceptionFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
            return payload ?? throw exceptionFactory(
                $"{serviceName} returned an empty response body.",
                null);
        }
        catch (JsonException exception)
        {
            throw exceptionFactory($"{serviceName} returned malformed JSON.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw exceptionFactory($"{serviceName} returned an unsupported payload.", exception);
        }
    }

    public static async Task<UpstreamProblemDetails?> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var title = GetString(root, "title");
            var detail = GetString(root, "detail");
            var extensions = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in root.EnumerateObject())
            {
                if (property.NameEquals("type") ||
                    property.NameEquals("title") ||
                    property.NameEquals("status") ||
                    property.NameEquals("detail") ||
                    property.NameEquals("instance") ||
                    property.NameEquals("traceId"))
                {
                    continue;
                }

                extensions[property.Name] = property.Value.Clone();
            }

            return new UpstreamProblemDetails(title, detail, extensions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
