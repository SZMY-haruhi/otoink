using Otoink.Core;

public class SettingsStoreTests
{
    [Fact]
    public void Save_then_Load_restores_default_ai_and_punctuation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "otoink-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var store = new SettingsStore(path);

        store.Save(new AppSettings
        {
            DefaultAiInput = true,
            AutoPunctuation = false,
            MicrophoneId = "mic-2",
            ApiBaseUrl = "https://api.deepseek.com/v1",
            ApiKey = "sk-test",
            ApiModel = "deepseek-chat",
            ApiPrompt = "保持书面语",
            ApiProvider = "anthropic",
            HoldHotkey = "RightCtrl",
            UiLocale = "en",
            AsrLanguage = "en",
            UiSkin = "ion"
        });

        var loaded = store.Load();
        Assert.True(loaded.DefaultAiInput);
        Assert.False(loaded.AutoPunctuation);
        Assert.Equal("mic-2", loaded.MicrophoneId);
        Assert.Equal("https://api.deepseek.com/v1", loaded.ApiBaseUrl);
        Assert.Equal("sk-test", loaded.ApiKey);
        Assert.Equal("deepseek-chat", loaded.ApiModel);
        Assert.Equal("保持书面语", loaded.ApiPrompt);
        Assert.Equal("anthropic", loaded.ApiProvider);
        Assert.Equal("RightCtrl", loaded.HoldHotkey);
        Assert.Equal("en", loaded.UiLocale);
        Assert.Equal("en", loaded.AsrLanguage);
        Assert.Equal("ion", loaded.UiSkin);
    }

    [Fact]
    public void Load_missing_file_returns_product_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "otoink-missing-" + Guid.NewGuid() + ".json");
        var loaded = new SettingsStore(path).Load();
        Assert.False(loaded.DefaultAiInput);
        Assert.True(loaded.AutoPunctuation);
        Assert.Equal("https://api.deepseek.com/v1", loaded.ApiBaseUrl);
        Assert.Equal("deepseek-chat", loaded.ApiModel);
        Assert.Equal("", loaded.ApiPrompt);
        Assert.Equal("openai", loaded.ApiProvider);
        Assert.Equal("RightCtrl", loaded.HoldHotkey);
        Assert.Equal("", loaded.ApiKey);
        Assert.Equal("", loaded.MicrophoneId);
        Assert.Equal("zh-Hans", loaded.UiLocale);
        Assert.Equal("zh", loaded.AsrLanguage);
        Assert.Equal("void", loaded.UiSkin);
    }
}
