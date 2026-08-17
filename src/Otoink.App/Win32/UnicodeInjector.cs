using System.Runtime.InteropServices;
using Otoink.Core;

namespace Otoink.App.Win32;

public sealed class UnicodeInjector : ITextInjector
{
    /// <summary>
    /// HWND captured at dictation start (mic/hotkey). History insert uses the last
    /// non-otoink window remembered by <see cref="RememberExternalForeground"/>.
    /// </summary>
    private IntPtr _target = IntPtr.Zero;
    private IntPtr _lastApp = IntPtr.Zero;

    public bool HasTarget => NativeMethods.IsWindow(_target);

    public void ClearTarget() => _target = IntPtr.Zero;

    public void CaptureForeground()
    {
        TryCaptureForeground(Array.Empty<IntPtr>());
    }

    public bool TryCaptureForeground(IEnumerable<IntPtr> ignore)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return NativeMethods.IsWindow(_target);

        foreach (var skipped in ignore)
        {
            if (hwnd == skipped)
                return NativeMethods.IsWindow(_target);
        }

        if (IsShellSurface(hwnd))
            return false;

        _target = hwnd;
        _lastApp = hwnd;
        return true;
    }

    public void RememberExternalForeground(IEnumerable<IntPtr> ignore)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IsShellSurface(hwnd) || !NativeMethods.IsWindow(hwnd))
            return;

        foreach (var skipped in ignore)
        {
            if (hwnd == skipped)
                return;
        }

        _lastApp = hwnd;
    }

    /// <summary>
    /// Focus the last real app window so SendInput lands there, not in Settings.
    /// </summary>
    public bool TryFocusLastApp()
    {
        var hwnd = NativeMethods.IsWindow(_lastApp) ? _lastApp : _target;
        if (!NativeMethods.IsWindow(hwnd) || IsShellSurface(hwnd))
            return false;

        _target = hwnd;
        return FocusWindow(hwnd);
    }

    private static bool IsShellSurface(IntPtr hwnd)
    {
        var className = NativeMethods.GetWindowClass(hwnd);
        return className is
            "Progman" or
            "WorkerW" or
            "Shell_TrayWnd" or
            "Shell_SecondaryTrayWnd" or
            "NotifyIconOverflowWindow";
    }

    public void Inject(string text)
    {
        if (_target != IntPtr.Zero)
            FocusWindow(_target);

        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                SendVk(0x0D); // VK_RETURN
                continue;
            }

            SendUnicode(ch);
        }
    }

    private static bool FocusWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return false;

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        var foreground = NativeMethods.GetForegroundWindow();
        var thisThread = NativeMethods.GetCurrentThreadId();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);

        var attachedFg = thisThread != foregroundThread
            && foreground != IntPtr.Zero
            && NativeMethods.AttachThreadInput(thisThread, foregroundThread, true);
        var attachedTarget = thisThread != targetThread
            && NativeMethods.AttachThreadInput(thisThread, targetThread, true);

        var focused = NativeMethods.SetForegroundWindow(hwnd);
        NativeMethods.SetFocus(hwnd);

        if (attachedFg)
            NativeMethods.AttachThreadInput(thisThread, foregroundThread, false);
        if (attachedTarget)
            NativeMethods.AttachThreadInput(thisThread, targetThread, false);

        return focused || NativeMethods.GetForegroundWindow() == hwnd;
    }

    private static void SendUnicode(char ch)
    {
        var down = CreateKeyInput(
            wVk: 0,
            wScan: ch,
            dwFlags: NativeMethods.KEYEVENTF_UNICODE);
        var up = CreateKeyInput(
            wVk: 0,
            wScan: ch,
            dwFlags: NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP);
        NativeMethods.SendInput(2, [down, up], Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendVk(ushort vk)
    {
        var down = CreateKeyInput(wVk: vk, wScan: 0, dwFlags: 0);
        var up = CreateKeyInput(wVk: vk, wScan: 0, dwFlags: NativeMethods.KEYEVENTF_KEYUP);
        NativeMethods.SendInput(2, [down, up], Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort wVk, ushort wScan, uint dwFlags) =>
        new()
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = wVk,
                    wScan = wScan,
                    dwFlags = dwFlags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
}
