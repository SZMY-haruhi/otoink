namespace Otoink.Core;

public static class SilenceSplit
{
    public const double FrameSeconds = 0.02;
    public const double MinPauseSeconds = 0.5;
    public const double MinSpeechSeconds = 0.12;
    public const float RmsThreshold = 0.012f;
    public const float PeakThreshold = 0.035f;

    public static IReadOnlyList<float[]> Split(float[] samples, int sampleRate)
    {
        if (samples.Length == 0 || sampleRate <= 0)
            return Array.Empty<float[]>();

        var frame = Math.Max(1, (int)Math.Round(sampleRate * FrameSeconds));
        var minPauseFrames = Math.Max(1, (int)Math.Round(MinPauseSeconds / FrameSeconds));
        var minSpeechSamples = (int)(sampleRate * MinSpeechSeconds);
        var frameCount = (samples.Length + frame - 1) / frame;

        var silent = new bool[frameCount];
        for (var f = 0; f < frameCount; f++)
        {
            var start = f * frame;
            var end = Math.Min(samples.Length, start + frame);
            silent[f] = IsSilent(samples, start, end);
        }

        var ranges = new List<(int Start, int End)>();
        var index = 0;
        while (index < frameCount)
        {
            while (index < frameCount && silent[index])
                index++;
            if (index >= frameCount)
                break;

            var speechStart = index;
            var speechEnd = index;
            while (speechEnd < frameCount)
            {
                while (speechEnd < frameCount && !silent[speechEnd])
                    speechEnd++;

                var pauseStart = speechEnd;
                var pauseEnd = pauseStart;
                while (pauseEnd < frameCount && silent[pauseEnd])
                    pauseEnd++;

                var pauseFrames = pauseEnd - pauseStart;
                if (pauseEnd >= frameCount || pauseFrames >= minPauseFrames)
                    break;

                speechEnd = pauseEnd;
            }

            var startSample = speechStart * frame;
            var endSample = Math.Min(samples.Length, speechEnd * frame);
            if (endSample - startSample >= minSpeechSamples)
                ranges.Add((startSample, endSample));

            index = speechEnd;
        }

        if (ranges.Count == 0)
            return Array.Empty<float[]>();

        var chunks = new float[ranges.Count][];
        for (var i = 0; i < ranges.Count; i++)
        {
            var length = ranges[i].End - ranges[i].Start;
            var chunk = new float[length];
            Array.Copy(samples, ranges[i].Start, chunk, 0, length);
            chunks[i] = chunk;
        }

        return chunks;
    }

    private static bool IsSilent(float[] samples, int start, int end)
    {
        var peak = 0f;
        double sumSq = 0;
        var count = end - start;
        for (var i = start; i < end; i++)
        {
            var abs = Math.Abs(samples[i]);
            if (abs > peak)
                peak = abs;
            sumSq += samples[i] * samples[i];
        }

        var rms = Math.Sqrt(sumSq / Math.Max(1, count));
        return peak < PeakThreshold && rms < RmsThreshold;
    }
}
