using Otoink.Core.I18n;

public class LocTests
{
    [Fact]
    public void Apply_en_makes_same_key_not_chinese()
    {
        Loc.Apply("zh-Hans");
        var zh = Loc.T("Bar.IdleTooltip");
        Assert.Contains("点击", zh);

        Loc.Apply("en");
        var en = Loc.T("Bar.IdleTooltip");
        Assert.DoesNotContain("点击", en);
        Assert.NotEqual(zh, en);
        Assert.Contains("Click", en, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_locale_falls_back_to_zh_Hans()
    {
        Loc.Apply("fr");
        Assert.Equal("zh-Hans", Loc.Current);
        Assert.Contains("点击", Loc.T("Bar.IdleTooltip"));
    }
}
