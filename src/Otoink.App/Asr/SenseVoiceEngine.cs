using Otoink.Core;
using SherpaOnnx;

namespace Otoink.App.Asr;

public sealed class SenseVoiceEngine : IAsrEngine, IDisposable
{
    private readonly Func<AppSettings> _settings;
    private readonly object _gate = new();
    private OfflineRecognizer? _recognizer;
    private bool? _autoPunctuation;
    private string? _language;

    public SenseVoiceEngine(Func<AppSettings> settings) => _settings = settings;

    public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureRecognizer();
                using var stream = _recognizer!.CreateStream();
                stream.AcceptWaveform(sampleRate, samples);
                _recognizer.Decode(stream);
                return stream.Result.Text ?? "";
            }
        }, cancellationToken);
    }

    public void Warmup()
    {
        lock (_gate)
        {
            EnsureRecognizer();
            using var stream = _recognizer!.CreateStream();
            stream.AcceptWaveform(16000, new float[16000]);
            _recognizer.Decode(stream);
        }
    }

    private void EnsureRecognizer()
    {
        var snapshot = _settings();
        var autoPunctuation = snapshot.AutoPunctuation;
        var language = AsrLanguage.Normalize(snapshot.AsrLanguage);
        if (_recognizer != null && _autoPunctuation == autoPunctuation && _language == language)
            return;

        _recognizer?.Dispose();
        _recognizer = null;

        if (!ModelLocator.IsInstalled())
            throw new InvalidOperationException("model-missing");

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = ModelLocator.Tokens;
        config.ModelConfig.SenseVoice.Model = ModelLocator.ResolveModelPath();
        config.ModelConfig.SenseVoice.Language = language;
        config.ModelConfig.SenseVoice.UseInverseTextNormalization = autoPunctuation ? 1 : 0;
        config.ModelConfig.NumThreads = Math.Clamp(Environment.ProcessorCount, 1, 4);
        config.DecodingMethod = "greedy_search";

        _recognizer = new OfflineRecognizer(config);
        _autoPunctuation = autoPunctuation;
        _language = language;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _recognizer?.Dispose();
            _recognizer = null;
        }
    }
}
