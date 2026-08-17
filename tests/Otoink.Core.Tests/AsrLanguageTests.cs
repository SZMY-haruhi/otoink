using Otoink.Core;

public class AsrLanguageTests
{
    [Theory]
    [InlineData(null, "zh")]
    [InlineData("", "zh")]
    [InlineData("zh", "zh")]
    [InlineData("ZH", "zh")]
    [InlineData("en", "en")]
    [InlineData("yue", "yue")]
    [InlineData("ja", "ja")]
    [InlineData("ko", "ko")]
    [InlineData("auto", "auto")]
    [InlineData("fr", "zh")]
    public void Normalize_maps_known_ids_and_falls_back_to_chinese(string? id, string expected)
    {
        Assert.Equal(expected, AsrLanguage.Normalize(id));
    }
}
