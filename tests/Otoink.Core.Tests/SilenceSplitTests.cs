using Otoink.Core;

public class SilenceSplitTests
{
    private const int SampleRate = 16000;

    [Fact]
    public void Split_two_phrases_separated_by_long_pause()
    {
        var samples = Concat(Tone(SampleRate), Silence(SampleRate * 0.6), Tone(SampleRate));
        var parts = SilenceSplit.Split(samples, SampleRate);
        Assert.Equal(2, parts.Count);
        Assert.InRange(parts[0].Length, (int)(SampleRate * 0.85), (int)(SampleRate * 1.15));
        Assert.InRange(parts[1].Length, (int)(SampleRate * 0.85), (int)(SampleRate * 1.15));
    }

    [Fact]
    public void Split_keeps_one_chunk_when_pause_is_short()
    {
        var samples = Concat(Tone(SampleRate), Silence(SampleRate * 0.2), Tone(SampleRate));
        var parts = SilenceSplit.Split(samples, SampleRate);
        Assert.Single(parts);
        Assert.InRange(parts[0].Length, (int)(SampleRate * 1.8), (int)(SampleRate * 2.3));
    }

    [Fact]
    public void Split_all_silence_returns_empty()
    {
        Assert.Empty(SilenceSplit.Split(Silence(SampleRate), SampleRate));
    }

    [Fact]
    public void Split_continuous_speech_returns_one_chunk()
    {
        var parts = SilenceSplit.Split(Tone(SampleRate * 2), SampleRate);
        Assert.Single(parts);
    }

    private static float[] Tone(int samples)
    {
        var data = new float[samples];
        Array.Fill(data, 0.2f);
        return data;
    }

    private static float[] Silence(double samples) => new float[(int)samples];

    private static float[] Concat(params float[][] parts)
    {
        var data = new float[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, data, offset * sizeof(float), part.Length * sizeof(float));
            offset += part.Length;
        }

        return data;
    }
}
