namespace Otoink.Core;

public sealed class TranscriptStore
{
    public const int MaxEntries = 10;

    private readonly List<TranscriptEntry> _items = new();

    public TranscriptEntry Add(string rawText)
    {
        var entry = new TranscriptEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.Now,
            RawText = rawText
        };
        _items.Add(entry);
        while (_items.Count > MaxEntries)
            _items.RemoveAt(0);
        return entry;
    }

    public TranscriptEntry UpdateCorrected(Guid id, string correctedText)
    {
        var entry = _items.FirstOrDefault(e => e.Id == id)
            ?? throw new KeyNotFoundException($"transcript {id} not found");
        entry.CorrectedText = correctedText;
        return entry;
    }

    public IReadOnlyList<TranscriptEntry> ListNewestFirst() =>
        _items.AsEnumerable().Reverse().ToList();
}
