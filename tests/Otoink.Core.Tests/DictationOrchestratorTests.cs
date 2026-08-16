using Otoink.Core;

public class DictationOrchestratorTests
{
    private readonly FakeAsr _asr = new("识别稿");
    private readonly FakeAi _ai = new("纠正稿");
    private readonly FakeInjector _injector = new();
    private readonly TranscriptStore _history = new();

    private DictationOrchestrator Create(bool defaultAiInput) =>
        new(_asr, _ai, _injector, _history, () => new AppSettings { DefaultAiInput = defaultAiInput });

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

    private sealed class FakeAsr : IAsrEngine
    {
        private readonly string _text;
        public FakeAsr(string text) => _text = text;
        public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken) =>
            Task.FromResult(_text);
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

    private sealed class FakeInjector : ITextInjector
    {
        public List<string> Injected { get; } = new();
        public void Inject(string text) => Injected.Add(text);
    }
}
