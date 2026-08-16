using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Otoink.App.Win32;
using Otoink.Core;

namespace Otoink.App;

public partial class MainWindow : Window
{
    private readonly UnicodeInjector _injector = new();
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private bool _settingsFlyoutOpen;

    public MainWindow(SettingsStore settingsStore, AppSettings settings)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        InitializeComponent();
        SettingsPopup.Opened += OnSettingsPopupOpened;
        SettingsPopup.Closed += OnSettingsPopupClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyNoActivateToolWindow();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnMicPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _injector.CaptureForeground();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (SettingsPopup.IsOpen)
        {
            SettingsPopup.IsOpen = false;
            return;
        }

        SettingsPopup.Child = new SettingsFlyout(_settingsStore, _settings);
        EnableKeyboardForSettings();
        SettingsPopup.IsOpen = true;
        _settingsFlyoutOpen = true;
        Activate();
    }

    private void OnSettingsPopupOpened(object? sender, EventArgs e)
    {
        // Popup HWND exists only after open + layout; clear its NOACTIVATE and focus it.
        Dispatcher.BeginInvoke(EnableKeyboardOnPopupHwnd, DispatcherPriority.Loaded);
    }

    private void OnSettingsPopupClosed(object? sender, EventArgs e)
    {
        if (!_settingsFlyoutOpen)
            return;
        _settingsFlyoutOpen = false;
        ApplyNoActivateToolWindow();
    }

    /// <summary>
    /// Temporarily clear WS_EX_NOACTIVATE on the owner so Popup TextBox/PasswordBox can receive keys.
    /// </summary>
    private void EnableKeyboardForSettings()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    /// <summary>
    /// WPF Popup creates its own HWND with WS_EX_NOACTIVATE; clear that and focus it.
    /// </summary>
    private void EnableKeyboardOnPopupHwnd()
    {
        if (SettingsPopup.Child is not { } child)
            return;

        if (PresentationSource.FromVisual(child) is not HwndSource source)
            return;

        var popupHwnd = source.Handle;
        if (popupHwnd == IntPtr.Zero)
            return;

        var exStyle = NativeMethods.GetWindowLongPtr(popupHwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(popupHwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);

        NativeMethods.SetForegroundWindow(popupHwnd);
        NativeMethods.SetFocus(popupHwnd);
    }

    /// <summary>
    /// Restore Task 7 bar behavior: no activation steal + tool window + topmost.
    /// </summary>
    private void ApplyNoActivateToolWindow()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
