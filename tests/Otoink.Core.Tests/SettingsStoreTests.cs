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
            HoldHotkey = "RightCtrl"
        });

        var loaded = store.Load();
        Assert.True(loaded.DefaultAiInput);
        Assert.False(loaded.AutoPunctuation);
        Assert.Equal("mic-2", loaded.MicrophoneId);
        Assert.Equal("https://api.deepseek.com/v1", loaded.ApiBaseUrl);
        Assert.Equal("sk-test", loaded.ApiKey);
        Assert.Equal("deepseek-chat", loaded.ApiModel);
        Assert.Equal("RightCtrl", loaded.HoldHotkey);
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
        Assert.Equal("RightCtrl", loaded.HoldHotkey);
        Assert.Equal("", loaded.ApiKey);
        Assert.Equal("", loaded.MicrophoneId);
    }
}
