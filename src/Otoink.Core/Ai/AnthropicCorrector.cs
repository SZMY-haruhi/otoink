using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Otoink.Core.Ai;

public sealed class AnthropicCorrector : IAiCorrector
{
    public const string ApiVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;

    public AnthropicCorrector(HttpClient http, Func<AppSettings> settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken)
    {
        var s = _settings();
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            throw new InvalidOperationException("API Key is not set");

        var prompt = string.IsNullOrWhiteSpace(s.ApiPrompt)
            ? OpenAiCompatibleCorrector.SystemPrompt
            : s.ApiPrompt.Trim();

        var req = new HttpRequestMessage(HttpMethod.Post, MessagesUri(s.ApiBaseUrl));
        req.Headers.TryAddWithoutValidation("x-api-key", s.ApiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        req.Content = JsonContent.Create(new MessagesRequest
        {
            Model = s.ApiModel,
            MaxTokens = 2048,
            Temperature = 0.2,
            System = prompt,
            Messages = [new MessagesTurn { Role = "user", Content = rawText }]
        }, options: JsonOptions);

        using var resp = await _http.SendAsync(req, cancellationToken);
        await ApiError.ThrowIfFailedAsync(resp, cancellationToken);
        var parsed = await resp.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("empty AI response");
        var content = parsed.Content?.FirstOrDefault(part => part.Type is null or "text")?.Text?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("AI returned no text");
        return content;
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var s = _settings();
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            throw new InvalidOperationException("API Key is not set");

        var req = new HttpRequestMessage(HttpMethod.Post, MessagesUri(s.ApiBaseUrl));
        req.Headers.TryAddWithoutValidation("x-api-key", s.ApiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        req.Content = JsonContent.Create(new MessagesRequest
        {
            Model = s.ApiModel,
            MaxTokens = 8,
            Temperature = 0,
            System = "Reply with OK.",
            Messages = [new MessagesTurn { Role = "user", Content = "ping" }]
        }, options: JsonOptions);

        using var resp = await _http.SendAsync(req, cancellationToken);
        await ApiError.ThrowIfFailedAsync(resp, cancellationToken);
    }

    public static Uri MessagesUri(string baseUrl)
    {
        var root = baseUrl.Trim().TrimEnd('/');
        if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return new Uri(root + "/messages");
        return new Uri(root + "/v1/messages");
    }

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("system")] public string System { get; set; } = "";
        [JsonPropertyName("messages")] public MessagesTurn[] Messages { get; set; } = [];
    }

    private sealed class MessagesTurn
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("content")] public MessagesPart[]? Content { get; set; }
    }

    private sealed class MessagesPart
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
