using Otoink.Core;

public class TranscriptNoiseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".。？.。..。")]
    [InlineData("[.。？.。..。Yeah.]")]
    [InlineData("Yeah.")]
    [InlineData("嗯")]
    [InlineData("啊啊啊")]
    [InlineData("uh")]
    public void Ignores_punctuation_and_filler(string text) =>
        Assert.True(TranscriptNoise.IsIgnorable(text));

    [Theory]
    [InlineData("喂能听到吗")]
    [InlineData("Yeah I think so")]
    [InlineData("好的")]
    public void Keeps_real_speech(string text) =>
        Assert.False(TranscriptNoise.IsIgnorable(text));

    [Theory]
    [InlineData("嗯，我今天去一趟", "我今天去一趟")]
    [InlineData("我今天，嗯，去一趟", "我今天去一趟")]
    [InlineData("我今天，呃，去一趟。", "我今天去一趟。")]
    [InlineData("嗯我今天去", "我今天去")]
    [InlineData("我嗯今天", "我今天")]
    [InlineData("好啊", "好啊")]
    [InlineData("对啊，走吧", "对啊，走吧")]
    [InlineData("那个文件", "那个文件")]
    [InlineData("天气，，真好", "天气，真好")]
    [InlineData("天气。。真好", "天气。真好")]
    [InlineData("我今天去一趟，", "我今天去一趟")]
    [InlineData("I, uh, think so", "I think so")]
    [InlineData("uh I think so", "I think so")]
    public void Clean_strips_mid_sentence_fillers_and_pause_commas(string raw, string expected) =>
        Assert.Equal(expected, TranscriptNoise.Clean(raw));

    [Fact]
    public void JoinChunks_drops_boundary_commas_from_thinking_pauses()
    {
        var joined = TranscriptNoise.JoinChunks(new[] { "我今天，", "去一趟" });
        Assert.Equal("我今天去一趟", joined);
    }

    [Fact]
    public void JoinChunks_skips_filler_only_pieces()
    {
        var joined = TranscriptNoise.JoinChunks(new[] { "嗯，", "打开设置" });
        Assert.Equal("打开设置", joined);
    }
}

public class AudioGateTests
{
    [Fact]
    public void Short_buffer_is_too_short()
    {
        Assert.True(AudioGate.IsTooShortOrQuiet(new float[100], 16000));
    }

    [Fact]
    public void Quiet_buffer_is_rejected()
    {
        Assert.True(AudioGate.IsTooShortOrQuiet(new float[8000], 16000));
    }

    [Fact]
    public void Loud_short_word_is_kept()
    {
        var samples = new float[8000];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = 0.2f;
        Assert.False(AudioGate.IsTooShortOrQuiet(samples, 16000));
    }
}
