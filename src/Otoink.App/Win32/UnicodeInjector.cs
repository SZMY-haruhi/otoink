using System.Runtime.InteropServices;
using Otoink.Core;

namespace Otoink.App.Win32;

public sealed class UnicodeInjector : ITextInjector
{
    /// <summary>
    /// HWND captured at dictation start (mic/hotkey). Manual「录入」recaptures via
    /// <see cref="CaptureForeground"/> on PreviewMouseLeftButtonDown before Insert.
    /// </summary>
    private IntPtr _target = IntPtr.Zero;

    public void CaptureForeground()
    {
        _target = NativeMethods.GetForegroundWindow();
    }

    public void Inject(string text)
    {
        if (_target != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(_target);

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
