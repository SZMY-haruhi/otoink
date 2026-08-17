namespace Otoink.Core;

public sealed class DictationSession
{
    public DictationPhase Phase { get; private set; } = DictationPhase.Idle;

    public event Action<DictationPhase, DictationPhase>? PhaseChanged;

    public DictationCommandResult HotkeyDown()
    {
        return Phase switch
        {
            DictationPhase.Idle => Set(DictationPhase.RecordingHold, DictationCommandResult.StartedHold),
            DictationPhase.RecordingToggle => Set(DictationPhase.Processing, DictationCommandResult.Submitted),
            _ => DictationCommandResult.Ignored
        };
    }

    public DictationCommandResult HotkeyUp()
    {
        if (Phase != DictationPhase.RecordingHold)
            return DictationCommandResult.Ignored;
        return Set(DictationPhase.Processing, DictationCommandResult.Submitted);
    }

    public DictationCommandResult PillClick()
    {
        if (Phase != DictationPhase.Idle)
            return DictationCommandResult.Ignored;
        return Set(DictationPhase.RecordingToggle, DictationCommandResult.StartedToggle);
    }

    public DictationCommandResult StopClick()
    {
        if (Phase != DictationPhase.RecordingToggle)
            return DictationCommandResult.Ignored;
        return Set(DictationPhase.Processing, DictationCommandResult.Submitted);
    }

    public DictationCommandResult CancelX()
    {
        if (Phase != DictationPhase.RecordingToggle)
            return DictationCommandResult.Ignored;
        return Set(DictationPhase.Idle, DictationCommandResult.Cancelled);
    }

    public DictationCommandResult FinishProcessing()
    {
        if (Phase != DictationPhase.Processing)
            return DictationCommandResult.Ignored;
        return Set(DictationPhase.Idle, DictationCommandResult.Finished);
    }

    private DictationCommandResult Set(DictationPhase next, DictationCommandResult result)
    {
        var previous = Phase;
        Phase = next;
        if (previous != next)
            PhaseChanged?.Invoke(previous, next);
        return result;
    }
}
