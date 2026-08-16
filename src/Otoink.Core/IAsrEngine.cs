namespace Otoink.Core;

public interface IAsrEngine
{
    Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken);
}
