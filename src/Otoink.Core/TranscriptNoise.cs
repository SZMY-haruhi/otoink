using System.Text;
using System.Text.RegularExpressions;

namespace Otoink.Core;

public static class TranscriptNoise
{
    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "yeah", "yea", "yep", "yap", "hmm", "mm", "mhm", "uh", "um", "ah", "oh", "huh",
        "嗯", "啊", "呃", "唔", "哼", "哦", "噢", "额", "呀", "哈", "嗯嗯", "啊啊"
    };

    private const string FillerChars = "嗯啊呃唔哼哦噢额呀哈";
    private const string Punctuation = ".。,，?？!！;；:：…·~、·•\"'“”‘’[]【】()（）<>《》-—_ \t\r\n";
    private const string PausePunct = "，,、;；";

    private static readonly Regex EnglishFiller = new(
        @"[\s,，、;；]*\b(?:uh|um|hmm|mhm)\b[\s,，、;；]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IsolatedChineseFiller = new(
        @"(?:[，,、;；\s]+)?(?<![\u4e00-\u9fffA-Za-z])[嗯呃唔额啊哦噢哼]+(?:[，,、;；\s]+)?",
        RegexOptions.Compiled);

    private static readonly Regex GluedChineseFiller = new(
        @"(?<=[\u4e00-\u9fff])[嗯呃唔额]+(?=[\u4e00-\u9fff])",
        RegexOptions.Compiled);

    private static readonly Regex LeadingFiller = new(
        @"^[，,、;；\s]*[嗯呃唔额啊哦噢哼]+[，,、;；\s]*",
        RegexOptions.Compiled);

    private static readonly Regex RepeatedComma = new(@"[，,]{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedPause = new(@"[、]{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedPeriod = new(@"[。.]{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedQuestion = new(@"[？?]{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedBang = new(@"[！!]{2,}", RegexOptions.Compiled);
    private static readonly Regex CommaThenPeriod = new(@"[，,]+[。.]", RegexOptions.Compiled);
    private static readonly Regex PeriodThenComma = new(@"[。.]+[，,]", RegexOptions.Compiled);
    private static readonly Regex ExtraSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforeCjkPunct = new(@"\s+([，。、！？；：])", RegexOptions.Compiled);
    private static readonly Regex SpaceAfterCjkOpen = new(@"([（【《])\s+", RegexOptions.Compiled);

    public static bool IsIgnorable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var stripped = StripPunctuation(Clean(text));
        if (stripped.Length == 0)
            return true;

        var tokens = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(IsFiller);
    }

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var value = text.Trim();
        value = EnglishFiller.Replace(value, " ");
        value = IsolatedChineseFiller.Replace(value, "");
        value = GluedChineseFiller.Replace(value, "");
        value = LeadingFiller.Replace(value, "");
        value = CollapsePunctuation(value);
        value = value.Trim();
        value = TrimPause(value);
        return CollapsePunctuation(value).Trim();
    }

    public static string JoinChunks(IReadOnlyList<string> chunks)
    {
        var parts = new List<string>();
        foreach (var chunk in chunks)
        {
            var cleaned = Clean(chunk);
            if (IsIgnorable(cleaned))
                continue;
            parts.Add(cleaned);
        }

        if (parts.Count == 0)
            return "";
        if (parts.Count == 1)
            return parts[0];

        var buffer = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            var piece = parts[i];
            if (i > 0)
            {
                TrimTrailingPause(buffer);
                piece = TrimLeadingPunct(piece);
                if (NeedsSpace(buffer, piece))
                    buffer.Append(' ');
            }

            buffer.Append(piece);
        }

        return Clean(buffer.ToString());
    }

    private static string CollapsePunctuation(string text)
    {
        var value = RepeatedComma.Replace(text, "，");
        value = RepeatedPause.Replace(value, "、");
        value = RepeatedPeriod.Replace(value, "。");
        value = RepeatedQuestion.Replace(value, "？");
        value = RepeatedBang.Replace(value, "！");
        value = CommaThenPeriod.Replace(value, "。");
        value = PeriodThenComma.Replace(value, "。");
        value = ExtraSpaces.Replace(value, " ");
        value = SpaceBeforeCjkPunct.Replace(value, "$1");
        value = SpaceAfterCjkOpen.Replace(value, "$1");
        return value;
    }

    private static string TrimPause(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && PausePunct.Contains(text[start]))
            start++;
        while (end > start && PausePunct.Contains(text[end - 1]))
            end--;
        return text[start..end];
    }

    private static void TrimTrailingPause(StringBuilder buffer)
    {
        while (buffer.Length > 0 && PausePunct.Contains(buffer[^1]))
            buffer.Length--;
    }

    private static string TrimLeadingPunct(string text)
    {
        var i = 0;
        while (i < text.Length && (PausePunct.Contains(text[i]) || text[i] is '.' or '。'))
            i++;
        return text[i..];
    }

    private static bool NeedsSpace(StringBuilder left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
            return false;
        return IsAsciiWord(left[^1]) && IsAsciiWord(right[0]);
    }

    private static bool IsAsciiWord(char ch) =>
        char.IsAsciiLetterOrDigit(ch);

    private static string StripPunctuation(string text)
    {
        var buffer = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (Punctuation.Contains(ch))
            {
                if (buffer.Length > 0 && buffer[^1] != ' ')
                    buffer.Append(' ');
                continue;
            }

            buffer.Append(ch);
        }

        return buffer.ToString().Trim();
    }

    private static bool IsFiller(string token)
    {
        if (Fillers.Contains(token))
            return true;
        return token.Length > 0 && token.All(ch => FillerChars.Contains(ch));
    }
}
