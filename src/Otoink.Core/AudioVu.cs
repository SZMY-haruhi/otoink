namespace Otoink.Core;

public static class AudioVu
{
    public static float FromPeakAndRms(float peak, float rms)
    {
        var fromRms = (rms - 0.018f) / 0.16f;
        var fromPeak = (peak - 0.035f) / 0.32f;
        var vu = Math.Max(fromRms, fromPeak);
        if (vu < 0.04f)
            return 0;
        return Math.Clamp(vu, 0, 1);
    }
}
