namespace Otoink.Core;

public sealed class DictationOrchestrator
{
    private readonly IAsrEngine _asr;
    private readonly IAiCorrector _ai;
    private readonly ITextInjector _injector;
    private readonly TranscriptStore _history;
    private readonly Func<AppSettings> _settings;

    public DictationOrchestrator(
        IAsrEngine asr,
        IAiCorrector ai,
        ITextInjector injector,
        TranscriptStore history,
        Func<AppSettings> settings)
    {
        _asr = asr;
        _ai = ai;
        _injector = injector;
        _history = history;
        _settings = settings;
    }

    public async Task<TranscriptEntry?> CompleteUtteranceAsync(DictationRequest request, CancellationToken cancellationToken)
    {
        var chunks = SilenceSplit.Split(request.Samples, request.SampleRate);
        if (chunks.Count == 0)
            chunks = new[] { request.Samples };

        var pieces = new string[chunks.Count];
        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pieces[i] = (await _asr.TranscribeAsync(chunks[i], request.SampleRate, cancellationToken)).Trim();
        }

        var raw = TranscriptNoise.JoinChunks(pieces);
        if (TranscriptNoise.IsIgnorable(raw))
            return null;
        return await CompleteTextAsync(raw, cancellationToken);
    }

    public async Task<TranscriptEntry?> CompleteTextAsync(string rawText, CancellationToken cancellationToken)
    {
        var raw = TranscriptNoise.Clean(rawText);
        if (TranscriptNoise.IsIgnorable(raw))
            return null;
        var entry = _history.Add(raw);
        var settings = _settings();
        if (settings.DefaultAiInput && !string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var corrected = await _ai.CorrectAsync(raw, cancellationToken);
            entry = _history.UpdateCorrected(entry.Id, corrected.Trim());
            _injector.Inject(entry.CorrectedText!);
        }
        else
        {
            _injector.Inject(raw);
        }
        return entry;
    }

    public async Task<TranscriptEntry> OptimizeAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = _history.ListNewestFirst().First(e => e.Id == id);
        var corrected = await _ai.CorrectAsync(entry.RawText, cancellationToken);
        return _history.UpdateCorrected(id, corrected.Trim());
    }

    public void Insert(Guid id)
    {
        var entry = _history.ListNewestFirst().First(e => e.Id == id);
        _injector.Inject(string.IsNullOrEmpty(entry.CorrectedText) ? entry.RawText : entry.CorrectedText);
    }

    public Task ProbeApiAsync(CancellationToken cancellationToken) =>
        _ai.ProbeAsync(cancellationToken);
}
