using System.Windows;
using System.Windows.Media.Animation;

namespace Otoink.App.Motion;

/// <summary>
/// CSS-style cubic-bezier. Dictation bars typically use (0.2, 0.8, 0.2, 1).
/// </summary>
internal sealed class CubicBezierEase : EasingFunctionBase
{
    private readonly double _x1;
    private readonly double _y1;
    private readonly double _x2;
    private readonly double _y2;

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
        EasingMode = EasingMode.EaseIn;
    }

    protected override Freezable CreateInstanceCore() =>
        new CubicBezierEase(_x1, _y1, _x2, _y2);

    protected override double EaseInCore(double t)
    {
        var s = t;
        for (var i = 0; i < 8; i++)
        {
            var x = Cubic(s, _x1, _x2) - t;
            var dx = CubicDx(s, _x1, _x2);
            if (Math.Abs(dx) < 1e-6)
                break;
            s = Math.Clamp(s - x / dx, 0, 1);
        }

        return Cubic(s, _y1, _y2);
    }

    private static double Cubic(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private static double CubicDx(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }
}
