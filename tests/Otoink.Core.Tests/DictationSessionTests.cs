using Otoink.Core;

public class DictationSessionTests
{
    [Fact]
    public void HotkeyDown_from_idle_starts_hold()
    {
        var session = new DictationSession();
        Assert.Equal(DictationPhase.Idle, session.Phase);
        Assert.Equal(DictationCommandResult.StartedHold, session.HotkeyDown());
        Assert.Equal(DictationPhase.RecordingHold, session.Phase);
    }

    [Fact]
    public void HotkeyUp_from_hold_submits_to_processing()
    {
        var session = new DictationSession();
        session.HotkeyDown();
        Assert.Equal(DictationCommandResult.Submitted, session.HotkeyUp());
        Assert.Equal(DictationPhase.Processing, session.Phase);
    }

    [Fact]
    public void PillClick_from_idle_starts_toggle()
    {
        var session = new DictationSession();
        Assert.Equal(DictationCommandResult.StartedToggle, session.PillClick());
        Assert.Equal(DictationPhase.RecordingToggle, session.Phase);
    }

    [Fact]
    public void StopClick_from_toggle_submits()
    {
        var session = new DictationSession();
        session.PillClick();
        Assert.Equal(DictationCommandResult.Submitted, session.StopClick());
        Assert.Equal(DictationPhase.Processing, session.Phase);
    }

    [Fact]
    public void HotkeyDown_during_toggle_submits_like_stop()
    {
        var session = new DictationSession();
        session.PillClick();
        Assert.Equal(DictationCommandResult.Submitted, session.HotkeyDown());
        Assert.Equal(DictationPhase.Processing, session.Phase);
    }

    [Fact]
    public void CancelX_from_toggle_returns_idle_without_submit()
    {
        var session = new DictationSession();
        session.PillClick();
        Assert.Equal(DictationCommandResult.Cancelled, session.CancelX());
        Assert.Equal(DictationPhase.Idle, session.Phase);
    }

    [Fact]
    public void CancelX_during_hold_is_ignored()
    {
        var session = new DictationSession();
        session.HotkeyDown();
        Assert.Equal(DictationCommandResult.Ignored, session.CancelX());
        Assert.Equal(DictationPhase.RecordingHold, session.Phase);
    }

    [Fact]
    public void Processing_ignores_start_stop_and_cancel()
    {
        var session = new DictationSession();
        session.HotkeyDown();
        session.HotkeyUp();
        Assert.Equal(DictationPhase.Processing, session.Phase);
        Assert.Equal(DictationCommandResult.Ignored, session.HotkeyDown());
        Assert.Equal(DictationCommandResult.Ignored, session.HotkeyUp());
        Assert.Equal(DictationCommandResult.Ignored, session.PillClick());
        Assert.Equal(DictationCommandResult.Ignored, session.StopClick());
        Assert.Equal(DictationCommandResult.Ignored, session.CancelX());
        Assert.Equal(DictationPhase.Processing, session.Phase);
    }

    [Fact]
    public void FinishProcessing_returns_idle()
    {
        var session = new DictationSession();
        session.HotkeyDown();
        session.HotkeyUp();
        Assert.Equal(DictationCommandResult.Finished, session.FinishProcessing());
        Assert.Equal(DictationPhase.Idle, session.Phase);
    }

    [Fact]
    public void HotkeyUp_from_idle_is_ignored()
    {
        var session = new DictationSession();
        Assert.Equal(DictationCommandResult.Ignored, session.HotkeyUp());
        Assert.Equal(DictationPhase.Idle, session.Phase);
    }
}
