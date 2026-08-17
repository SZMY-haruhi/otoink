namespace Otoink.Core;

public enum DictationPhase
{
    Idle,
    RecordingHold,
    RecordingToggle,
    Processing
}

public enum DictationCommandResult
{
    Ignored,
    StartedHold,
    StartedToggle,
    Submitted,
    Cancelled,
    Finished
}
