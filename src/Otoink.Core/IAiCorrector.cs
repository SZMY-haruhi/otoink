namespace Otoink.Core;

public interface IAiCorrector
{
    Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken);
}
