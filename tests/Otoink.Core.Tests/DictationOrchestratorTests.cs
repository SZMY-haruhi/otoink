using Otoink.Core;

public class DictationOrchestratorTests
{
    private readonly FakeAsr _asr = new("识别稿");
    private readonly FakeAi _ai = new("纠正稿");
    private readonly FakeInjector _injector = new();
    private readonly TranscriptStore _history = new();

    private DictationOrchestrator Create(bool defaultAiInput) =>
        new(_asr, _ai, _injector, _history, () => new AppSettings
        {
            DefaultAiInput = defaultAiInput,
            ApiKey = defaultAiInput ? "sk-test" : ""
        });

    [Fact]
    public async Task Complete_without_default_ai_injects_raw_and_does_not_call_ai()
    {
        var session = Create(defaultAiInput: false);
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);

        Assert.Equal("识别稿", entry.RawText);
        Assert.Null(entry.CorrectedText);
        Assert.Equal(new[] { "识别稿" }, _injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Complete_with_default_ai_injects_corrected_only()
    {
        var session = Create(defaultAiInput: true);
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);

        Assert.Equal("识别稿", entry.RawText);
        Assert.Equal("纠正稿", entry.CorrectedText);
        Assert.Equal(new[] { "纠正稿" }, _injector.Injected);
        Assert.Equal(1, _ai.Calls);
    }

    [Fact]
    public async Task Optimize_updates_entry_but_does_not_inject()
    {
        var session = Create(defaultAiInput: false);
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);
        _injector.Injected.Clear();

        var updated = await session.OptimizeAsync(entry.Id, CancellationToken.None);
        Assert.Equal("纠正稿", updated.CorrectedText);
        Assert.Empty(_injector.Injected);
    }

    [Fact]
    public async Task Insert_uses_corrected_when_present_otherwise_raw()
    {
        var session = Create(defaultAiInput: false);
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);
        await session.OptimizeAsync(entry.Id, CancellationToken.None);
        _injector.Injected.Clear();

        session.Insert(entry.Id);
        Assert.Equal(new[] { "纠正稿" }, _injector.Injected);
    }

    [Fact]
    public async Task CompleteText_without_default_ai_injects_raw_and_does_not_call_ai()
    {
        var session = Create(defaultAiInput: false);
        var entry = await session.CompleteTextAsync("系统识别稿", CancellationToken.None);

        Assert.Equal("系统识别稿", entry.RawText);
        Assert.Equal(new[] { "系统识别稿" }, _injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task CompleteText_strips_mid_sentence_fillers_before_inject()
    {
        var session = Create(defaultAiInput: false);
        var entry = await session.CompleteTextAsync("嗯，系统识别稿", CancellationToken.None);

        Assert.Equal("系统识别稿", entry!.RawText);
        Assert.Equal(new[] { "系统识别稿" }, _injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Complete_splits_thinking_pause_and_joins_without_comma()
    {
        var asr = new ScriptedAsr("我今天，", "去一趟");
        var session = new DictationOrchestrator(asr, _ai, _injector, _history, () => new AppSettings());
        var samples = Concat(Tone(16000), new float[9600], Tone(16000));

        var entry = await session.CompleteUtteranceAsync(
            new DictationRequest { Samples = samples, SampleRate = 16000 },
            CancellationToken.None);

        Assert.Equal("我今天去一趟", entry!.RawText);
        Assert.Equal(new[] { "我今天去一趟" }, _injector.Injected);
        Assert.Equal(2, asr.Calls);
    }

    [Fact]
    public async Task Complete_with_default_ai_and_empty_key_injects_raw_without_calling_ai()
    {
        var session = new DictationOrchestrator(
            _asr,
            _ai,
            _injector,
            _history,
            () => new AppSettings { DefaultAiInput = true, ApiKey = "" });

        var entry = await session.CompleteUtteranceAsync(
            new DictationRequest { Samples = new float[160], SampleRate = 16000 },
            CancellationToken.None);

        Assert.Equal("识别稿", entry!.RawText);
        Assert.Null(entry.CorrectedText);
        Assert.Equal(new[] { "识别稿" }, _injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Complete_filler_asr_does_not_inject_or_store()
    {
        var asr = new FakeAsr("[.。？.。..。Yeah.]");
        var session = new DictationOrchestrator(asr, _ai, _injector, _history, () => new AppSettings());
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);
        Assert.Null(entry);
        Assert.Empty(_history.ListNewestFirst());
        Assert.Empty(_injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Complete_empty_asr_does_not_inject_or_call_ai()
    {
        var asr = new FakeAsr("   ");
        var session = new DictationOrchestrator(asr, _ai, _injector, _history, () => new AppSettings());
        var entry = await session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None);
        Assert.Null(entry);
        Assert.Empty(_history.ListNewestFirst());
        Assert.Empty(_injector.Injected);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Complete_with_default_ai_when_ai_throws_keeps_raw_and_does_not_inject()
    {
        var ai = new ThrowingAi();
        var session = new DictationOrchestrator(_asr, ai, _injector, _history, () => new AppSettings { DefaultAiInput = true, ApiKey = "sk-test" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.CompleteUtteranceAsync(new DictationRequest { Samples = new float[160], SampleRate = 16000 }, CancellationToken.None));

        var entries = _history.ListNewestFirst().ToList();
        Assert.Single(entries);
        Assert.Equal("识别稿", entries[0].RawText);
        Assert.Null(entries[0].CorrectedText);
        Assert.Empty(_injector.Injected);
        Assert.Equal(1, ai.Calls);
    }

    private sealed class FakeAsr : IAsrEngine
    {
        private readonly string _text;
        public FakeAsr(string text) => _text = text;
        public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken) =>
            Task.FromResult(_text);
    }

    private sealed class ScriptedAsr : IAsrEngine
    {
        private readonly string[] _texts;
        public int Calls { get; private set; }
        public ScriptedAsr(params string[] texts) => _texts = texts;
        public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken)
        {
            var text = _texts[Math.Min(Calls, _texts.Length - 1)];
            Calls++;
            return Task.FromResult(text);
        }
    }

    private static float[] Tone(int samples)
    {
        var data = new float[samples];
        Array.Fill(data, 0.2f);
        return data;
    }

    private static float[] Concat(params float[][] parts)
    {
        var data = new float[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Array.Copy(part, 0, data, offset, part.Length);
            offset += part.Length;
        }

        return data;
    }

    private sealed class FakeAi : IAiCorrector
    {
        private readonly string _text;
        public int Calls { get; private set; }
        public FakeAi(string text) => _text = text;
        public Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_text);
        }
    }

    private sealed class ThrowingAi : IAiCorrector
    {
        public int Calls { get; private set; }
        public Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("AI unavailable");
        }
    }

    private sealed class FakeInjector : ITextInjector
    {
        public List<string> Injected { get; } = new();
        public void Inject(string text) => Injected.Add(text);
    }
}
