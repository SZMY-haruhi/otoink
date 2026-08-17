namespace Otoink.Core;

public static class AudioGate
{
    public static bool IsTooShortOrQuiet(float[] samples, int sampleRate)
    {
        if (samples.Length < sampleRate * 0.22)
            return true;

        double sumSq = 0;
        var peak = 0f;
        foreach (var sample in samples)
        {
            var abs = Math.Abs(sample);
            if (abs > peak)
                peak = abs;
            sumSq += sample * sample;
        }

        var rms = Math.Sqrt(sumSq / samples.Length);
        return peak < 0.035 && rms < 0.012;
    }
}
