namespace Otoink.Core.Ai;

public sealed class LlmCorrector : IAiCorrector
{
    private readonly IAiCorrector _openAi;
    private readonly IAiCorrector _anthropic;
    private readonly Func<AppSettings> _settings;

    public LlmCorrector(IAiCorrector openAi, IAiCorrector anthropic, Func<AppSettings> settings)
    {
        _openAi = openAi;
        _anthropic = anthropic;
        _settings = settings;
    }

    public Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken) =>
        Current().CorrectAsync(rawText, cancellationToken);

    public Task ProbeAsync(CancellationToken cancellationToken) =>
        Current().ProbeAsync(cancellationToken);

    private IAiCorrector Current() =>
        ApiProvider.IsAnthropic(_settings().ApiProvider) ? _anthropic : _openAi;
}
