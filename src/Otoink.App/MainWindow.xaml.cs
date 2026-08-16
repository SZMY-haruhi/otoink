using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Otoink.App.Asr;
using Otoink.App.Audio;
using Otoink.App.Tray;
using Otoink.App.Win32;
using Otoink.Core;

namespace Otoink.App;

public partial class MainWindow : Window
{
    private enum RecordingSource
    {
        None,
        MicButton,
        HoldHotkey
    }

    private const double CollapsedHeight = 96;
    private const double ExpandedHeight = 96 + 240;
    private const string ModelWaitingMessage = "请等待模型下载";
    private const string ModelDownloadingMessage = "正在下载识别模型…";

    private static readonly Brush IdleMicBackground = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush RecordingMicBackground = Freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)));
    private static readonly Brush IdleMicIconBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7F, 0xDB, 0xFF)));
    private static readonly Brush RecordingMicIconBrush = Brushes.White;

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private readonly UnicodeInjector _injector;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly TranscriptStore _history;
    private readonly DictationOrchestrator _orchestrator;
    private readonly HistoryPanel _historyPanel;
    private readonly MicrophoneRecorder _recorder = new();
    private readonly NotifyIconService _tray;
    private HoldHotkeyHook? _holdHotkey;
    private RecordingSource _source = RecordingSource.None;
    private bool _settingsFlyoutOpen;
    private bool _historyExpanded;
    private bool _utteranceBusy;
    private bool _forceClose;
    private bool _modelDownloading;

    public MainWindow(
        SettingsStore settingsStore,
        AppSettings settings,
        TranscriptStore history,
        DictationOrchestrator orchestrator,
        UnicodeInjector injector,
        NotifyIconService tray)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _history = history;
        _orchestrator = orchestrator;
        _injector = injector;
        _tray = tray;
        InitializeComponent();
        _historyPanel = new HistoryPanel(_history, _orchestrator);
        HistoryHost.Child = _historyPanel;
        SettingsPopup.Opened += OnSettingsPopupOpened;
        SettingsPopup.Closed += OnSettingsPopupClosed;
        _recorder.Stopped += OnRecorderStopped;
        _tray.ShowRequested += OnTrayShowRequested;
        _tray.ExitRequested += OnTrayExitRequested;
        Closed += OnWindowClosed;
        if (!ModelLocator.IsInstalled())
            MicButton.IsEnabled = false;
    }

    public void BeginModelDownload()
    {
        void Apply()
        {
            _modelDownloading = true;
            MicButton.IsEnabled = false;
            MicButton.ToolTip = ModelDownloadingMessage;
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    public void EndModelDownload(bool success, string? error = null)
    {
        void Apply()
        {
            _modelDownloading = false;
            if (success && ModelLocator.IsInstalled())
            {
                MicButton.IsEnabled = true;
                MicButton.ToolTip = "Dictate";
            }
            else
            {
                MicButton.IsEnabled = false;
                MicButton.ToolTip = string.IsNullOrWhiteSpace(error)
                    ? ModelWaitingMessage
                    : error;
            }
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _recorder.Stopped -= OnRecorderStopped;
        _tray.ShowRequested -= OnTrayShowRequested;
        _tray.ExitRequested -= OnTrayExitRequested;
        _holdHotkey?.Dispose();
        _holdHotkey = null;
        _recorder.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyNoActivateToolWindow();
        InstallHoldHotkey();
    }

    private void InstallHoldHotkey()
    {
        _holdHotkey?.Dispose();
        _holdHotkey = new HoldHotkeyHook(HoldHotkeyHook.ResolveVirtualKey(_settings.HoldHotkey));
        _holdHotkey.KeyDown += OnHoldHotkeyKeyDown;
        _holdHotkey.KeyUp += OnHoldHotkeyKeyUp;
        _holdHotkey.Install();
    }

    private void OnHoldHotkeyKeyDown()
    {
        Dispatcher.BeginInvoke(OnHoldHotkeyPressed, DispatcherPriority.Send);
    }

    private void OnHoldHotkeyKeyUp()
    {
        Dispatcher.BeginInvoke(OnHoldHotkeyReleased, DispatcherPriority.Send);
    }

    private void OnHoldHotkeyPressed()
    {
        if (_utteranceBusy || _recorder.IsRecording)
        {
            if (!IsVisible)
                RestoreWindow();
            return;
        }

        if (_modelDownloading || !ModelLocator.IsInstalled())
        {
            if (!IsVisible)
                RestoreWindow();
            ShowModelWaiting();
            return;
        }

        MicButton.ToolTip = "Dictate";
        // Capture target HWND before Show() can steal foreground.
        _injector.CaptureForeground();
        if (!IsVisible)
            RestoreWindow();

        try
        {
            _recorder.Start(ResolveDeviceNumber());
            _source = RecordingSource.HoldHotkey;
            SetRecordingVisual(true);
        }
        catch (Exception ex)
        {
            _source = RecordingSource.None;
            SetRecordingVisual(false);
            MicButton.ToolTip = ex.Message;
        }
    }

    private void OnHoldHotkeyReleased()
    {
        if (_source != RecordingSource.HoldHotkey)
            return;

        if (_recorder.IsRecording)
            _recorder.Stop();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnMicPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_utteranceBusy)
            return;

        if (_recorder.IsRecording)
        {
            if (_source == RecordingSource.MicButton)
                _recorder.Stop();
            return;
        }

        if (_modelDownloading || !ModelLocator.IsInstalled())
        {
            ShowModelWaiting();
            return;
        }

        MicButton.ToolTip = "Dictate";
        _injector.CaptureForeground();
        try
        {
            _recorder.Start(ResolveDeviceNumber());
            _source = RecordingSource.MicButton;
            SetRecordingVisual(true);
        }
        catch (Exception ex)
        {
            _source = RecordingSource.None;
            SetRecordingVisual(false);
            MicButton.ToolTip = ex.Message;
        }
    }

    private async void OnRecorderStopped(float[] samples, int sampleRate)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnRecorderStopped(samples, sampleRate));
            return;
        }

        _source = RecordingSource.None;
        SetRecordingVisual(false);

        if (samples.Length == 0)
            return;

        if (!ModelLocator.IsInstalled())
        {
            ShowModelWaiting();
            return;
        }

        _utteranceBusy = true;
        try
        {
            var entry = await _orchestrator.CompleteUtteranceAsync(
                new DictationRequest { Samples = samples, SampleRate = sampleRate },
                CancellationToken.None);

            // Silence / empty ASR → null; ignore without error.
            _ = entry;
            _historyPanel.Refresh();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("模型", StringComparison.Ordinal))
        {
            ShowModelWaiting();
        }
        catch (Exception ex)
        {
            MicButton.ToolTip = ex.Message;
        }
        finally
        {
            _utteranceBusy = false;
        }
    }

    private void ShowModelWaiting()
    {
        MicButton.ToolTip = _modelDownloading ? ModelDownloadingMessage : ModelWaitingMessage;
        if (_historyExpanded)
            _historyPanel.Refresh();
    }

    private int ResolveDeviceNumber()
    {
        if (string.IsNullOrEmpty(_settings.MicrophoneId))
            return 0;
        return int.TryParse(_settings.MicrophoneId, out var device) ? device : 0;
    }

    private void SetRecordingVisual(bool recording)
    {
        MicButton.Background = recording ? RecordingMicBackground : IdleMicBackground;
        MicIcon.Fill = recording ? RecordingMicIconBrush : IdleMicIconBrush;
        MicButton.ToolTip = recording ? "Stop" : "Dictate";
    }

    private void OnHistoryToggleClick(object sender, RoutedEventArgs e)
    {
        _historyExpanded = !_historyExpanded;
        if (_historyExpanded)
        {
            _historyPanel.Refresh();
            HistoryHost.Visibility = Visibility.Visible;
            Height = ExpandedHeight;
            HistoryToggleRotate.Angle = 180;
            HistoryToggleButton.ToolTip = "Collapse";
        }
        else
        {
            HistoryHost.Visibility = Visibility.Collapsed;
            Height = CollapsedHeight;
            HistoryToggleRotate.Angle = 0;
            HistoryToggleButton.ToolTip = "History";
        }
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
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void OnTrayShowRequested()
    {
        Dispatcher.BeginInvoke(RestoreWindow, DispatcherPriority.Send);
    }

    private void OnTrayExitRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _forceClose = true;
            System.Windows.Application.Current.Shutdown();
        }, DispatcherPriority.Send);
    }

    private void RestoreWindow()
    {
        Show();
        Topmost = true;
        ApplyNoActivateToolWindow();
    }
}
