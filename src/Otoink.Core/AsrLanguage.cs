namespace Otoink.Core;

public static class AsrLanguage
{
    public const string Chinese = "zh";
    public const string English = "en";
    public const string Cantonese = "yue";
    public const string Japanese = "ja";
    public const string Korean = "ko";
    public const string Auto = "auto";

    public static readonly string[] All =
    [
        Chinese,
        English,
        Cantonese,
        Japanese,
        Korean,
        Auto
    ];

    public static string Normalize(string? id)
    {
        var value = (id ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            English => English,
            Cantonese => Cantonese,
            Japanese => Japanese,
            Korean => Korean,
            Auto => Auto,
            _ => Chinese
        };
    }
}
