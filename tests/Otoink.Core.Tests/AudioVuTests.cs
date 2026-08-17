using Otoink.Core;

public class AudioVuTests
{
    [Fact]
    public void Silence_maps_to_zero()
    {
        Assert.Equal(0, AudioVu.FromPeakAndRms(0.02f, 0.008f));
    }

    [Fact]
    public void Speech_maps_to_visible_level()
    {
        var vu = AudioVu.FromPeakAndRms(0.22f, 0.09f);
        Assert.InRange(vu, 0.4f, 1f);
    }

    [Fact]
    public void Loud_speech_reaches_top()
    {
        Assert.Equal(1, AudioVu.FromPeakAndRms(0.6f, 0.28f));
    }
}
