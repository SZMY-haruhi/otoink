using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Otoink.Core.Ai;

public sealed class OpenAiCompatibleCorrector : IAiCorrector
{
    public const string SystemPrompt =
        "你是中文语音转写校对器。纠正错别字、补标点、理顺口语，不要扩写，不要解释，只返回纠正后的正文。";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;

    public OpenAiCompatibleCorrector(HttpClient http, Func<AppSettings> settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken)
    {
        var s = _settings();
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            throw new InvalidOperationException("API Key is not set");

        var prompt = string.IsNullOrWhiteSpace(s.ApiPrompt) ? SystemPrompt : s.ApiPrompt.Trim();
        var req = new HttpRequestMessage(HttpMethod.Post, Combine(s.ApiBaseUrl, "/chat/completions"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);
        req.Content = JsonContent.Create(new ChatRequest
        {
            Model = s.ApiModel,
            Temperature = 0.2,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = prompt },
                new ChatMessage { Role = "user", Content = rawText }
            }
        }, options: JsonOptions);

        using var resp = await _http.SendAsync(req, cancellationToken);
        await ApiError.ThrowIfFailedAsync(resp, cancellationToken);
        var parsed = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("empty AI response");
        var content = parsed.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("AI returned no text");
        return content;
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var s = _settings();
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            throw new InvalidOperationException("API Key is not set");

        var req = new HttpRequestMessage(HttpMethod.Post, Combine(s.ApiBaseUrl, "/chat/completions"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);
        req.Content = JsonContent.Create(new ChatRequest
        {
            Model = s.ApiModel,
            Temperature = 0,
            MaxTokens = 8,
            Messages =
            [
                new ChatMessage { Role = "system", Content = "Reply with OK." },
                new ChatMessage { Role = "user", Content = "ping" }
            ]
        }, options: JsonOptions);

        using var resp = await _http.SendAsync(req, cancellationToken);
        await ApiError.ThrowIfFailedAsync(resp, cancellationToken);
    }

    private static Uri Combine(string baseUrl, string path)
    {
        var root = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(root), path.TrimStart('/'));
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
        [JsonPropertyName("messages")] public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public ChatChoice[]? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }
}
