using Otoink.Core;
using SherpaOnnx;

namespace Otoink.App.Asr;

public sealed class SenseVoiceEngine : IAsrEngine, IDisposable
{
    private readonly Func<AppSettings> _settings;
    private OfflineRecognizer? _recognizer;
    private bool? _autoPunctuation;

    public SenseVoiceEngine(Func<AppSettings> settings) => _settings = settings;

    public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRecognizer();
            using var stream = _recognizer!.CreateStream();
            stream.AcceptWaveform(sampleRate, samples);
            _recognizer.Decode(stream);
            return stream.Result.Text ?? "";
        }, cancellationToken);
    }

    private void EnsureRecognizer()
    {
        var autoPunctuation = _settings().AutoPunctuation;
        if (_recognizer != null && _autoPunctuation == autoPunctuation)
            return;

        _recognizer?.Dispose();
        _recognizer = null;

        if (!ModelLocator.IsInstalled())
            throw new InvalidOperationException("SenseVoice 模型未安装");

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = ModelLocator.Tokens;
        config.ModelConfig.SenseVoice.Model = ModelLocator.ResolveModelPath();
        config.ModelConfig.SenseVoice.UseInverseTextNormalization = autoPunctuation ? 1 : 0;
        config.ModelConfig.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        config.DecodingMethod = "greedy_search";

        _recognizer = new OfflineRecognizer(config);
        _autoPunctuation = autoPunctuation;
    }

    public void Dispose()
    {
        _recognizer?.Dispose();
        _recognizer = null;
    }
}
