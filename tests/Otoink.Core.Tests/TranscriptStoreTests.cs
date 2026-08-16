using Otoink.Core;

public class TranscriptStoreTests
{
    [Fact]
    public void Add_then_UpdateCorrected_keeps_raw_and_sets_corrected()
    {
        var store = new TranscriptStore();
        var entry = store.Add("你好世界");
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal("你好世界", entry.RawText);
        Assert.Null(entry.CorrectedText);

        var updated = store.UpdateCorrected(entry.Id, "你好，世界。");
        Assert.Equal("你好世界", updated.RawText);
        Assert.Equal("你好，世界。", updated.CorrectedText);
        Assert.Equal("你好，世界。", store.ListNewestFirst().Single().CorrectedText);
    }

    [Fact]
    public void ListNewestFirst_returns_latest_added_first()
    {
        var store = new TranscriptStore();
        store.Add("one");
        store.Add("two");
        var texts = store.ListNewestFirst().Select(e => e.RawText).ToArray();
        Assert.Equal(new[] { "two", "one" }, texts);
    }

    [Fact]
    public void UpdateCorrected_unknown_id_throws()
    {
        var store = new TranscriptStore();
        Assert.Throws<KeyNotFoundException>(() => store.UpdateCorrected(Guid.NewGuid(), "x"));
    }
}
