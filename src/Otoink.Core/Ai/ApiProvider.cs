namespace Otoink.Core.Ai;

public static class ApiProvider
{
    public const string OpenAiCompatible = "openai";
    public const string Anthropic = "anthropic";

    public const string OpenAiDefaultUrl = "https://api.deepseek.com/v1";
    public const string OpenAiDefaultModel = "deepseek-chat";
    public const string AnthropicDefaultUrl = "https://api.anthropic.com";
    public const string AnthropicDefaultModel = "claude-sonnet-4-5";

    public static string Normalize(string? id) =>
        string.Equals(id, Anthropic, StringComparison.OrdinalIgnoreCase)
            ? Anthropic
            : OpenAiCompatible;

    public static bool IsAnthropic(string? id) => Normalize(id) == Anthropic;

    public static bool IsStockUrl(string? url, string provider)
    {
        var value = (url ?? "").Trim().TrimEnd('/');
        if (IsAnthropic(provider))
            return value.Equals(AnthropicDefaultUrl, StringComparison.OrdinalIgnoreCase)
                || value.Equals(AnthropicDefaultUrl + "/v1", StringComparison.OrdinalIgnoreCase);

        return value.Equals(OpenAiDefaultUrl, StringComparison.OrdinalIgnoreCase)
            || value.Equals("https://api.openai.com/v1", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStockModel(string? model, string provider)
    {
        var value = (model ?? "").Trim();
        return IsAnthropic(provider)
            ? value.Equals(AnthropicDefaultModel, StringComparison.OrdinalIgnoreCase)
            : value.Equals(OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase);
    }
}
