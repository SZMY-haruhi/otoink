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
        var raw = (await _asr.TranscribeAsync(request.Samples, request.SampleRate, cancellationToken)).Trim();
        if (raw.Length == 0)
            return null;
        var entry = _history.Add(raw);
        if (_settings().DefaultAiInput)
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
}
