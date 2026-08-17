using System.Windows;
using System.Windows.Media;

namespace Otoink.App.Theme;

public static class UiTheme
{
    public const string Void = "void";
    public const string Ember = "ember";
    public const string Ion = "ion";
    public const string Lunar = "lunar";

    public static event Action? Changed;

    public static string Current { get; private set; } = Void;

    public static IReadOnlyList<string> All { get; } = [Void, Ember, Ion, Lunar];

    public static string Normalize(string? id) => id switch
    {
        Ember => Ember,
        Ion => Ion,
        Lunar => Lunar,
        _ => Void
    };

    public static Color AccentOf(string? id) => For(Normalize(id)).Accent;

    public static void Apply(string? id)
    {
        Current = Normalize(id);
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var p = For(Current);
        SetBrush(app, "AppBg", p.AppBg);
        SetBrush(app, "ChromeBg", p.Chrome);
        SetBrush(app, "InnerBg", p.Inner);
        SetBrush(app, "RailBg", p.Rail);
        SetBrush(app, "FlyoutBg", p.Inner);
        SetBrush(app, "PopupBg", p.Popup);
        SetBrush(app, "RowBg", p.Row);
        SetBrush(app, "RowLine", p.RowLine);
        SetBrush(app, "TextPrimary", p.Text);
        SetBrush(app, "TextSecondary", p.Muted);
        SetBrush(app, "Accent", p.Accent);
        SetBrush(app, "AccentSoft", p.AccentSoft);
        SetBrush(app, "Danger", p.Danger);
        SetBrush(app, "FieldBg", p.Field);
        SetBrush(app, "FieldStroke", p.Stroke);
        SetBrush(app, "Stroke", p.Stroke);
        SetBrush(app, "PillFill", p.Pill);
        SetBrush(app, "PillStroke", p.PillStroke);
        SetBrush(app, "NavOn", p.NavOn);
        SetBrush(app, "NavHover", p.NavHover);
        SetBrush(app, "HandleFill", p.Handle);
        SetBrush(app, "SwitchOff", p.SwitchOff);
        SetBrush(app, "HintFill", p.Hint);
        app.Resources["AccentColor"] = p.Accent;
        app.Resources["AccentSoftColor"] = p.AccentSoft;
        Changed?.Invoke();
    }

    private static void SetBrush(System.Windows.Application app, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        app.Resources[key] = brush;
    }

    private static Palette For(string id) => id switch
    {
        Ember => new Palette(
            AppBg: Rgb(0x00, 0x00, 0x00),
            Chrome: Rgb(0x00, 0x00, 0x00),
            Inner: Rgb(0x00, 0x00, 0x00),
            Rail: Rgb(0x00, 0x00, 0x00),
            Popup: Rgb(0x16, 0x12, 0x10),
            Row: Rgb(0x00, 0x00, 0x00),
            RowLine: Rgb(0x2F, 0x33, 0x36),
            Text: Rgb(0xE7, 0xE9, 0xEA),
            Muted: Rgb(0x71, 0x76, 0x7B),
            Accent: Rgb(0xFF, 0x7A, 0x3A),
            AccentSoft: Rgb(0xFF, 0xC4, 0x96),
            Danger: Rgb(0xF4, 0x21, 0x2E),
            Field: Rgb(0x16, 0x18, 0x1C),
            Stroke: Rgb(0x2F, 0x33, 0x36),
            Pill: Color.FromArgb(0xF2, 0x00, 0x00, 0x00),
            PillStroke: Color.FromArgb(0x66, 0xE7, 0xE9, 0xEA),
            NavOn: Color.FromArgb(0x00, 0x00, 0x00, 0x00),
            NavHover: Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
            Handle: Rgb(0x71, 0x76, 0x7B),
            SwitchOff: Rgb(0x27, 0x2A, 0x2E),
            Hint: Color.FromArgb(0x00, 0x00, 0x00, 0x00)),
        Ion => new Palette(
            AppBg: Rgb(0x00, 0x00, 0x00),
            Chrome: Rgb(0x00, 0x00, 0x00),
            Inner: Rgb(0x00, 0x00, 0x00),
            Rail: Rgb(0x00, 0x00, 0x00),
            Popup: Rgb(0x12, 0x16, 0x1E),
            Row: Rgb(0x00, 0x00, 0x00),
            RowLine: Rgb(0x2F, 0x33, 0x36),
            Text: Rgb(0xE7, 0xE9, 0xEA),
            Muted: Rgb(0x71, 0x76, 0x7B),
            Accent: Rgb(0x6E, 0xA8, 0xFF),
            AccentSoft: Rgb(0xC0, 0xD8, 0xFF),
            Danger: Rgb(0xF4, 0x21, 0x2E),
            Field: Rgb(0x16, 0x18, 0x1C),
            Stroke: Rgb(0x2F, 0x33, 0x36),
            Pill: Color.FromArgb(0xF2, 0x00, 0x00, 0x00),
            PillStroke: Color.FromArgb(0x66, 0xE7, 0xE9, 0xEA),
            NavOn: Color.FromArgb(0x00, 0x00, 0x00, 0x00),
            NavHover: Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
            Handle: Rgb(0x71, 0x76, 0x7B),
            SwitchOff: Rgb(0x27, 0x2A, 0x2E),
            Hint: Color.FromArgb(0x00, 0x00, 0x00, 0x00)),
        Lunar => new Palette(
            AppBg: Rgb(0x00, 0x00, 0x00),
            Chrome: Rgb(0x00, 0x00, 0x00),
            Inner: Rgb(0x00, 0x00, 0x00),
            Rail: Rgb(0x00, 0x00, 0x00),
            Popup: Rgb(0x16, 0x18, 0x1C),
            Row: Rgb(0x00, 0x00, 0x00),
            RowLine: Rgb(0x2F, 0x33, 0x36),
            Text: Rgb(0xE7, 0xE9, 0xEA),
            Muted: Rgb(0x71, 0x76, 0x7B),
            Accent: Rgb(0xE7, 0xE9, 0xEA),
            AccentSoft: Rgb(0xF5, 0xF6, 0xF7),
            Danger: Rgb(0xF4, 0x21, 0x2E),
            Field: Rgb(0x16, 0x18, 0x1C),
            Stroke: Rgb(0x2F, 0x33, 0x36),
            Pill: Color.FromArgb(0xF2, 0x00, 0x00, 0x00),
            PillStroke: Color.FromArgb(0x66, 0xE7, 0xE9, 0xEA),
            NavOn: Color.FromArgb(0x00, 0x00, 0x00, 0x00),
            NavHover: Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
            Handle: Rgb(0x71, 0x76, 0x7B),
            SwitchOff: Rgb(0x27, 0x2A, 0x2E),
            Hint: Color.FromArgb(0x00, 0x00, 0x00, 0x00)),
        _ => new Palette(
            AppBg: Rgb(0x00, 0x00, 0x00),
            Chrome: Rgb(0x00, 0x00, 0x00),
            Inner: Rgb(0x00, 0x00, 0x00),
            Rail: Rgb(0x00, 0x00, 0x00),
            Popup: Rgb(0x16, 0x18, 0x1C),
            Row: Rgb(0x00, 0x00, 0x00),
            RowLine: Rgb(0x2F, 0x33, 0x36),
            Text: Rgb(0xE7, 0xE9, 0xEA),
            Muted: Rgb(0x71, 0x76, 0x7B),
            Accent: Rgb(0xFF, 0x6A, 0x3D),
            AccentSoft: Rgb(0xE7, 0xE9, 0xEA),
            Danger: Rgb(0xF4, 0x21, 0x2E),
            Field: Rgb(0x16, 0x18, 0x1C),
            Stroke: Rgb(0x2F, 0x33, 0x36),
            Pill: Color.FromArgb(0xF2, 0x00, 0x00, 0x00),
            PillStroke: Color.FromArgb(0x55, 0xE7, 0xE9, 0xEA),
            NavOn: Color.FromArgb(0x00, 0x00, 0x00, 0x00),
            NavHover: Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
            Handle: Rgb(0x71, 0x76, 0x7B),
            SwitchOff: Rgb(0x27, 0x2A, 0x2E),
            Hint: Color.FromArgb(0x00, 0x00, 0x00, 0x00))
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private readonly record struct Palette(
        Color AppBg,
        Color Chrome,
        Color Inner,
        Color Rail,
        Color Popup,
        Color Row,
        Color RowLine,
        Color Text,
        Color Muted,
        Color Accent,
        Color AccentSoft,
        Color Danger,
        Color Field,
        Color Stroke,
        Color Pill,
        Color PillStroke,
        Color NavOn,
        Color NavHover,
        Color Handle,
        Color SwitchOff,
        Color Hint);
}
