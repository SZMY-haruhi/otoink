namespace Otoink.Core;

public sealed class DictationRequest
{
    public float[] Samples { get; init; } = Array.Empty<float>();
    public int SampleRate { get; init; } = 16000;
}
