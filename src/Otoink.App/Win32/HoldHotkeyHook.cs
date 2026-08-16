using System.Runtime.InteropServices;

namespace Otoink.App.Win32;

/// <summary>
/// Low-level keyboard hook for hold-to-talk. <c>RegisterHotKey</c> cannot see key-up.
/// </summary>
public sealed class HoldHotkeyHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfUp = 0x80;

    public const ushort VkRControl = 0xA3;

    private readonly ushort _vk;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _isDown;
    private bool _disposed;

    public HoldHotkeyHook(ushort virtualKey = VkRControl)
    {
        _vk = virtualKey;
        // Keep delegate alive for the native callback.
        _proc = HookCallback;
    }

    public event Action? KeyDown;
    public event Action? KeyUp;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hookId != IntPtr.Zero)
            return;

        // WH_KEYBOARD_LL: hMod may be null when the proc lives in this process.
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, IntPtr.Zero, 0);

        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()}).");
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _isDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (info.VkCode == _vk)
            {
                var isUp = msg is WmKeyUp or WmSysKeyUp
                    || (info.Flags & LlkhfUp) != 0;
                var isDown = msg is WmKeyDown or WmSysKeyDown;

                if (isDown && !isUp)
                {
                    if (!_isDown)
                    {
                        _isDown = true;
                        KeyDown?.Invoke();
                    }
                }
                else if (isUp)
                {
                    if (_isDown)
                    {
                        _isDown = false;
                        KeyUp?.Invoke();
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Uninstall();
        KeyDown = null;
        KeyUp = null;
    }

    public static ushort ResolveVirtualKey(string? holdHotkey) =>
        string.Equals(holdHotkey, "RightCtrl", StringComparison.OrdinalIgnoreCase)
            ? VkRControl
            : VkRControl;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
