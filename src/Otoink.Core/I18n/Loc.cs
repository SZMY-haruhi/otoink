using System.Globalization;
using System.Resources;

namespace Otoink.Core.I18n;

public static class Loc
{
    public const string ZhHans = "zh-Hans";
    public const string En = "en";

    private static readonly ResourceManager Resources =
        new("Otoink.Core.I18n.Strings", typeof(Loc).Assembly);

    public static event Action? Changed;

    public static string Current { get; private set; } = ZhHans;

    public static void Apply(string? locale)
    {
        var normalized = Normalize(locale);
        var culture = new CultureInfo(normalized);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        var changed = !string.Equals(Current, normalized, StringComparison.Ordinal);
        Current = normalized;
        if (changed)
            Changed?.Invoke();
    }

    public static string Normalize(string? locale) =>
        string.Equals(locale, En, StringComparison.OrdinalIgnoreCase) ? En : ZhHans;

    public static string T(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, T(key), args);
}
