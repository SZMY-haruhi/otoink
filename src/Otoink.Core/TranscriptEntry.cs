namespace Otoink.Core;

public sealed class TranscriptEntry
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string RawText { get; init; } = "";
    public string? CorrectedText { get; set; }
}
