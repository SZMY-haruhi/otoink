namespace Otoink.Core;

public sealed class AppSettings
{
    public bool DefaultAiInput { get; set; }
    public bool AutoPunctuation { get; set; } = true;
    public string MicrophoneId { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";
    public string ApiKey { get; set; } = "";
    public string ApiModel { get; set; } = "deepseek-chat";
    public string ApiPrompt { get; set; } = "";
    public string ApiProvider { get; set; } = "openai";
    public string HoldHotkey { get; set; } = "RightCtrl";
    public string UiLocale { get; set; } = "zh-Hans";
    public string UiSkin { get; set; } = "void";
}
