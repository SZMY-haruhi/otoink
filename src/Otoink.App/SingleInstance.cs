namespace Otoink.App;

internal static class SingleInstance
{
    internal const string MutexName = @"Local\SZMY.otoink.single-instance";
    internal const string ActivateEventName = @"Local\SZMY.otoink.activate";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activate;
    private static CancellationTokenSource? _listen;

    public static bool TryTake()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            if (!_mutex.WaitOne(TimeSpan.Zero))
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
        }
        catch (AbandonedMutexException)
        {
            // Previous process crashed; this instance now owns the mutex.
        }

        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return true;
    }

    public static void RequestActivate()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(ActivateEventName);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    public static void Listen(Action onActivate)
    {
        if (_activate is null)
            return;

        _listen = new CancellationTokenSource();
        var token = _listen.Token;
        var wait = _activate;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (!wait.WaitOne(TimeSpan.FromMilliseconds(400)))
                    continue;
                if (!token.IsCancellationRequested)
                    onActivate();
            }
        }, token);
    }

    public static void Release()
    {
        _listen?.Cancel();
        _activate?.Dispose();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _mutex.Dispose();
        }

        _listen = null;
        _activate = null;
        _mutex = null;
    }
}
