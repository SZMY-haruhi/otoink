using System.Net.Http;
using System.Text.Json;

namespace Otoink.Core.Ai;

internal static class ApiError
{
    public static async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryMessage(body);
        var status = (int)response.StatusCode;
        throw new InvalidOperationException(string.IsNullOrEmpty(detail)
            ? $"HTTP {status}"
            : $"HTTP {status}: {detail}");
    }

    private static string? TryMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return Trim(error.GetString());
                if (error.TryGetProperty("message", out var nested))
                    return Trim(nested.GetString());
            }

            if (root.TryGetProperty("message", out var top))
                return Trim(top.GetString());
        }
        catch (JsonException)
        {
        }

        var flat = body.Trim();
        return flat.Length <= 180 ? flat : flat[..180];
    }

    private static string? Trim(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
