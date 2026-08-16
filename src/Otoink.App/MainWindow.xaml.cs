using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Otoink.App.Win32;
using Otoink.Core;

namespace Otoink.App;

public partial class MainWindow : Window
{
    private readonly UnicodeInjector _injector = new();
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public MainWindow(SettingsStore settingsStore, AppSettings settings)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        InitializeComponent();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
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
        SettingsPopup.IsOpen = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
