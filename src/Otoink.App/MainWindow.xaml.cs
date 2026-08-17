using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Otoink.App.Asr;
using Otoink.App.Audio;
using Otoink.App.Motion;
using Otoink.App.Theme;
using Otoink.App.Win32;
using Otoink.Core;
using Otoink.Core.I18n;

namespace Otoink.App;

public partial class MainWindow : Window
{
    private static readonly Duration Morph = new(TimeSpan.FromMilliseconds(240));
    private static readonly IEasingFunction Ease = CreateEase();

    private static IEasingFunction CreateEase()
    {
        var ease = new CubicBezierEase(0.16, 1, 0.3, 1);
        ease.Freeze();
        return ease;
    }

    private const int BarCount = 24;
    private const double IdleW = 52;
    private const double IdleH = 7;
    private const double HoverW = 248;
    private const double HoverH = 36;
    private const double HoldW = 148;
    private const double ToggleW = 228;
    private const double ActiveH = 44;
    private const double BottomGap = 8;
    private const float VoiceGate = 0.07f;

    private readonly UnicodeInjector _injector;
    private readonly AppSettings _settings;
    private readonly DictationOrchestrator _orchestrator;
    private readonly MicrophoneRecorder _recorder = new();
    private readonly DictationSession _session = new();
    private readonly DispatcherTimer _toastTimer;
    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _barHeights = new double[BarCount];
    private readonly float[] _history = new float[BarCount];
    private HoldHotkeyHook? _holdHotkey;
    private bool _hover;
    private bool _forceClose;
    private bool _skipUtterance;
    private bool _engineReady;
    private bool _toastOpen;
    private bool _glowOn;
    private bool _dotsOn;
    private bool _waveHooked;
    private bool _vuGlow;
    private float _peak;
    private float _env;
    private double _phase;
    private double _shiftAcc;
    private TimeSpan _lastRender;
    private HwndSource? _hwndSource;
    private Brush _waveFill = Brushes.Orange;
    private Brush _waveFillPeak = Brushes.White;
    private Func<IEnumerable<IntPtr>>? _ignoreHwnds;

    internal bool PreviewMode { get; set; }

    public event Action? TranscriptChanged;
    public Action? OpenSettings { get; set; }

    public MainWindow(
        AppSettings settings,
        DictationOrchestrator orchestrator,
        UnicodeInjector injector,
        Func<IEnumerable<IntPtr>>? ignoreHwnds = null,
        bool previewMode = false)
    {
        PreviewMode = previewMode;
        _settings = settings;
        _orchestrator = orchestrator;
        _injector = injector;
        _ignoreHwnds = ignoreHwnds;
        InitializeComponent();
        ApplyThemePaint();
        BuildWaveBars();
        UiTheme.Changed += OnThemeChanged;
        _recorder.Stopped += OnRecorderStopped;
        _recorder.Level += OnRecorderLevel;
        Closed += OnWindowClosed;
        Loc.Changed += OnLocChanged;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.15) };
        _toastTimer.Tick += (_, _) => HideToast();
        _engineReady = false;
        ApplyHoverText();
        ApplyVisual();
    }

    public void SetIgnoreHwnds(Func<IEnumerable<IntPtr>> ignoreHwnds) =>
        _ignoreHwnds = ignoreHwnds;

    internal void PreviewIdle()
    {
        HideToast();
        _session.FinishProcessing();
        while (_session.Phase is DictationPhase.RecordingHold or DictationPhase.RecordingToggle)
        {
            if (_session.Phase == DictationPhase.RecordingHold)
                _session.HotkeyUp();
            else
                _session.CancelX();
            _session.FinishProcessing();
        }

        _hover = false;
        ApplyVisual();
    }

    internal void PreviewHold()
    {
        PreviewIdle();
        _session.HotkeyDown();
        ApplyVisual();
    }

    internal void PreviewToggle()
    {
        PreviewIdle();
        _session.PillClick();
        ApplyVisual();
    }

    internal void PreviewProcessing()
    {
        PreviewIdle();
        _session.HotkeyDown();
        _session.HotkeyUp();
        ApplyVisual();
    }

    internal void PreviewToast()
    {
        PreviewIdle();
        ShowToast(Loc.T("Toast.NeedApiKey"));
    }

    internal IEnumerable<IntPtr> PreviewCaptureHwnds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            yield return hwnd;
    }

    internal string PreviewSizeText()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return $"DIP {ActualWidth:0}×{ActualHeight:0}   px {(int)Math.Ceiling(ActualWidth * dpi.DpiScaleX)}×{(int)Math.Ceiling(ActualHeight * dpi.DpiScaleY)}   scale {dpi.DpiScaleX:0.##}x";
    }

    public void BeginWarmup()
    {
        void Apply() => ShowToast(Loc.T("Toast.WarmingUp"), persist: true);
        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    public void EndWarmup(bool success, string? error = null)
    {
        void Apply()
        {
            _engineReady = success && ModelLocator.IsInstalled();
            HideToast();
            if (!success)
                ShowToast(MapError(error));
            else
                ShowToast(Loc.T("Toast.Ready"));
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    public void NotifyModelMissing()
    {
        void Apply() => ShowToast(Loc.T("Toast.MissingModel"), persist: true);
        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    public void ShowToast(string message, bool persist = false)
    {
        ToastText.Text = MapError(message);
        _toastOpen = true;
        ToastBar.Visibility = Visibility.Visible;
        Animate(ToastBar, OpacityProperty, 1);
        _toastTimer.Stop();
        if (!persist)
            _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        if (!_toastOpen)
            return;
        _toastOpen = false;
        var fade = new DoubleAnimation(ToastBar.Opacity, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = Ease
        };
        fade.Completed += (_, _) =>
        {
            if (!_toastOpen)
                ToastBar.Visibility = Visibility.Collapsed;
        };
        ToastBar.BeginAnimation(OpacityProperty, fade);
    }

    private void OnLocChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyHoverText();
            ApplyVisual();
        });
    }

    private void ApplyHoverText()
    {
        HintText.Text = Loc.T("Bar.IdleTooltip");
        KeyHintText.Text = FormatHoldKey(_settings.HoldHotkey);
        CancelButton.ToolTip = Loc.T("Bar.Cancel");
        StopButton.ToolTip = Loc.T("Bar.Stop");
    }

    private static string FormatHoldKey(string? holdHotkey) =>
        holdHotkey switch
        {
            "RightCtrl" => "Ctrl",
            "LeftCtrl" => "Ctrl",
            _ => string.IsNullOrWhiteSpace(holdHotkey) ? "Ctrl" : holdHotkey
        };

    private string MapError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Loc.T("Toast.EngineNotReady");
        if (message == "model-missing"
            || message.Contains("模型", StringComparison.Ordinal)
            || message.Contains("model", StringComparison.OrdinalIgnoreCase))
            return Loc.T("Toast.MissingModel");
        if (message.Contains("API Key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ApiKey", StringComparison.OrdinalIgnoreCase))
            return Loc.T("Toast.NeedApiKey");
        if (message.Length > 72)
            return message[..72] + "…";
        return message;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void BuildWaveBars()
    {
        WaveCanvas.Children.Clear();
        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = new Rectangle
            {
                Width = 3.2,
                Height = 4,
                RadiusX = 1.6,
                RadiusY = 1.6,
                Fill = _waveFill,
                Opacity = 0.92
            };
            _bars[i] = bar;
            _barHeights[i] = 4;
            WaveCanvas.Children.Add(bar);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Loc.Changed -= OnLocChanged;
        UiTheme.Changed -= OnThemeChanged;
        _recorder.Stopped -= OnRecorderStopped;
        _recorder.Level -= OnRecorderLevel;
        SetWaveRunning(false);
        _hwndSource?.RemoveHook(HitTestHook);
        _hwndSource = null;
        _holdHotkey?.Dispose();
        _holdHotkey = null;
        _recorder.Dispose();
        _toastTimer.Stop();
        StopGlow();
        StopDots();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PreviewMode)
        {
            ShowInTaskbar = true;
            return;
        }

        ApplyNoActivateToolWindow();
        InstallHoldHotkey();
        PlaceOnBottom();
        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _hwndSource?.AddHook(HitTestHook);
    }

    private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_NCHITTEST)
            return IntPtr.Zero;

        Point local;
        try
        {
            local = PointFromScreen(NativeMethods.ScreenPointFromLParam(lParam));
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }

        if (VisualTreeHelper.HitTest(this, local) is null)
        {
            handled = true;
            return NativeMethods.HTTRANSPARENT;
        }

        return IntPtr.Zero;
    }

    private void OnPillSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var h = Pill.ActualHeight;
        if (h < 1)
            return;
        var r = Math.Max(h / 2, 1);
        Pill.CornerRadius = new CornerRadius(r);
        PillRing.CornerRadius = new CornerRadius(r + 1);
    }

    private void PlaceOnBottom()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Bottom - Height - BottomGap;
    }

    private void InstallHoldHotkey()
    {
        _holdHotkey?.Dispose();
        _holdHotkey = new HoldHotkeyHook(HoldHotkeyHook.ResolveVirtualKey(_settings.HoldHotkey));
        _holdHotkey.KeyDown += () => Dispatcher.BeginInvoke(OnHoldHotkeyPressed, DispatcherPriority.Send);
        _holdHotkey.KeyUp += () => Dispatcher.BeginInvoke(OnHoldHotkeyReleased, DispatcherPriority.Send);
        _holdHotkey.Install();
    }

    private void OnHoldHotkeyPressed()
    {
        if (_session.Phase == DictationPhase.Idle && !TryLockTarget())
            return;

        var result = _session.HotkeyDown();
        if (result == DictationCommandResult.StartedHold)
            StartRecording();
        else if (result == DictationCommandResult.Submitted)
            SubmitRecording();
        ApplyVisual();
    }

    private void OnHoldHotkeyReleased()
    {
        if (_session.HotkeyUp() == DictationCommandResult.Submitted)
            SubmitRecording();
        ApplyVisual();
    }

    private void OnPillMouseEnter(object sender, MouseEventArgs e)
    {
        _hover = true;
        ApplyVisual();
    }

    private void OnPillMouseLeave(object sender, MouseEventArgs e)
    {
        _hover = false;
        ApplyVisual();
    }

    private void OnPillPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            return;
        if (_session.Phase != DictationPhase.Idle)
            return;
        if (!TryLockTarget())
            return;
        if (_session.PillClick() == DictationCommandResult.StartedToggle)
            StartRecording();
        ApplyVisual();
        e.Handled = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_session.CancelX() != DictationCommandResult.Cancelled)
            return;
        _skipUtterance = true;
        _injector.ClearTarget();
        if (_recorder.IsRecording)
            _recorder.Stop();
        else
            ApplyVisual();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_session.StopClick() == DictationCommandResult.Submitted)
            SubmitRecording();
        ApplyVisual();
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private bool TryLockTarget()
    {
        var ignore = new List<IntPtr>();
        var self = new WindowInteropHelper(this).Handle;
        if (self != IntPtr.Zero)
            ignore.Add(self);
        if (_ignoreHwnds != null)
            ignore.AddRange(_ignoreHwnds());

        if (_injector.TryCaptureForeground(ignore))
            return true;

        ShowToast(Loc.T("Toast.FocusFirst"));
        return false;
    }

    private bool CanRecord()
    {
        if (!ModelLocator.IsInstalled())
        {
            ShowToast(Loc.T("Toast.MissingModel"), persist: true);
            return false;
        }

        if (!_engineReady)
        {
            ShowToast(Loc.T("Toast.WarmingUp"), persist: true);
            return false;
        }

        return true;
    }

    private void StartRecording()
    {
        if (!CanRecord())
        {
            if (_session.Phase == DictationPhase.RecordingHold)
                _session.HotkeyUp();
            else if (_session.Phase == DictationPhase.RecordingToggle)
                _session.CancelX();
            _session.FinishProcessing();
            ApplyVisual();
            return;
        }

        try
        {
            _skipUtterance = false;
            _recorder.Start(ResolveDeviceNumber());
        }
        catch (Exception ex)
        {
            _skipUtterance = true;
            if (_session.Phase == DictationPhase.RecordingToggle)
                _session.CancelX();
            else if (_session.Phase == DictationPhase.RecordingHold)
            {
                _session.HotkeyUp();
                _session.FinishProcessing();
            }

            ShowToast(ex.Message);
        }
    }

    private void SubmitRecording()
    {
        _skipUtterance = false;
        if (_recorder.IsRecording)
            _recorder.Stop();
        else
            _ = FinishEmptyAsync();
    }

    private async Task FinishEmptyAsync()
    {
        _session.FinishProcessing();
        ApplyVisual();
        await Task.CompletedTask;
    }

    private async void OnRecorderStopped(float[] samples, int sampleRate)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnRecorderStopped(samples, sampleRate));
            return;
        }

        if (_skipUtterance)
        {
            _skipUtterance = false;
            _session.FinishProcessing();
            ApplyVisual();
            return;
        }

        if (samples.Length == 0 || AudioGate.IsTooShortOrQuiet(samples, sampleRate))
        {
            _session.FinishProcessing();
            ApplyVisual();
            return;
        }

        if (!ModelLocator.IsInstalled())
        {
            _session.FinishProcessing();
            ApplyVisual();
            ShowToast(Loc.T("Toast.MissingModel"), persist: true);
            return;
        }

        ApplyVisual();
        try
        {
            var entry = await _orchestrator.CompleteUtteranceAsync(
                new DictationRequest { Samples = samples, SampleRate = sampleRate },
                CancellationToken.None);
            if (entry is null)
                return;
            if (_settings.DefaultAiInput && string.IsNullOrWhiteSpace(_settings.ApiKey))
                ShowToast(Loc.T("Toast.NeedApiKey"));
            TranscriptChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowToast(ex.Message);
        }
        finally
        {
            _session.FinishProcessing();
            ApplyVisual();
        }
    }

    private void OnRecorderLevel(float peak) => _peak = peak;

    private void SetWaveRunning(bool on)
    {
        if (on == _waveHooked)
            return;
        _waveHooked = on;
        if (on)
        {
            _env = 0;
            _peak = 0;
            _phase = 0;
            _shiftAcc = 0;
            _lastRender = TimeSpan.Zero;
            Array.Clear(_history);
            CompositionTarget.Rendering += OnWaveFrame;
        }
        else
            CompositionTarget.Rendering -= OnWaveFrame;
    }

    private void OnWaveFrame(object? sender, EventArgs e)
    {
        if (_session.Phase is not (DictationPhase.RecordingHold or DictationPhase.RecordingToggle))
            return;

        var now = e is RenderingEventArgs render ? render.RenderingTime : TimeSpan.Zero;
        var dt = _lastRender == TimeSpan.Zero ? 0.016 : (now - _lastRender).TotalSeconds;
        _lastRender = now;
        if (dt <= 0 || dt > 0.08)
            dt = 0.016;

        var follow = _peak >= _env ? 26f : 5.2f;
        _env += (_peak - _env) * Math.Min(1f, (float)(dt * follow));

        _shiftAcc += dt;
        while (_shiftAcc >= 0.02)
        {
            _shiftAcc -= 0.02;
            Array.Copy(_history, 1, _history, 0, BarCount - 1);
            _history[^1] = _env;
        }

        var histMax = 0f;
        for (var i = 0; i < BarCount; i++)
        {
            if (_history[i] > histMax)
                histMax = _history[i];
        }

        var voiced = _env > VoiceGate || histMax > VoiceGate;
        if (_vuGlow)
            GlowHalo.Opacity = voiced ? 0.16 + Math.Clamp(_env, 0, 1) * 0.62 : 0.1;

        var width = WaveCanvas.ActualWidth;
        if (width < 8)
            return;

        var gap = width / BarCount;
        if (!voiced)
            _phase += dt * 4.2;
        var pulseAt = (_phase % BarCount + BarCount) % BarCount;

        for (var i = 0; i < BarCount; i++)
        {
            double target;
            double opacity;
            Brush fill;
            double barW;
            if (!voiced)
            {
                var dist = Math.Abs(i - pulseAt);
                dist = Math.Min(dist, BarCount - dist);
                var bump = Math.Exp(-dist * dist * 0.55);
                target = 3.2 + 4.8 * bump;
                opacity = 0.28 + 0.72 * bump;
                fill = _waveFill;
                barW = 3.6;
            }
            else
            {
                var v = Math.Clamp(_history[i], 0, 1);
                target = 3.6 + v * 22;
                opacity = 0.5 + 0.5 * v;
                fill = v > 0.55 ? _waveFillPeak : _waveFill;
                barW = 3.2;
            }

            _barHeights[i] += (target - _barHeights[i]) * Math.Min(1, dt * 22);
            var bar = _bars[i];
            bar.Width = barW;
            bar.Height = _barHeights[i];
            bar.RadiusX = barW / 2;
            bar.RadiusY = barW / 2;
            bar.Opacity = opacity;
            bar.Fill = fill;
            Canvas.SetLeft(bar, i * gap + (gap - bar.Width) / 2);
            Canvas.SetTop(bar, (28 - bar.Height) / 2);
        }
    }

    private int ResolveDeviceNumber()
    {
        if (string.IsNullOrEmpty(_settings.MicrophoneId))
            return 0;
        return int.TryParse(_settings.MicrophoneId, out var device) ? device : 0;
    }

    private void ApplyVisual()
    {
        var phase = _session.Phase;
        var hover = _hover && phase == DictationPhase.Idle;
        double pillW;
        double pillH;
        var showCancel = false;
        var showWave = false;
        var showDots = false;
        var showHint = false;
        var glow = false;

        switch (phase)
        {
            case DictationPhase.RecordingHold:
                pillW = HoldW;
                pillH = ActiveH;
                showWave = true;
                glow = true;
                break;
            case DictationPhase.RecordingToggle:
                pillW = ToggleW;
                pillH = ActiveH;
                showCancel = true;
                showWave = true;
                glow = true;
                break;
            case DictationPhase.Processing:
                pillW = HoldW;
                pillH = ActiveH;
                showDots = true;
                glow = true;
                break;
            default:
                if (hover)
                {
                    pillW = HoverW;
                    pillH = HoverH;
                    showHint = true;
                }
                else
                {
                    pillW = IdleW;
                    pillH = IdleH;
                }

                break;
        }

        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        StopButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        CancelCol.Width = showCancel ? new GridLength(36) : new GridLength(0);
        StopCol.Width = showCancel ? new GridLength(36) : new GridLength(0);
        WaveCanvas.Visibility = showWave ? Visibility.Visible : Visibility.Collapsed;
        Dots.Visibility = showDots ? Visibility.Visible : Visibility.Collapsed;
        Animate(HintText, OpacityProperty, showHint ? 1 : 0);
        Animate(KeyHint, OpacityProperty, showHint ? 1 : 0);
        SetWaveRunning(showWave);
        _vuGlow = showWave;

        if (showDots)
            StartDots();
        else
            StopDots();

        if (showWave)
            StopGlow();
        else if (glow)
            StartGlow();
        else
            StopGlow();

        PillRing.Background = glow
            ? SoftAccentStroke()
            : (Brush)FindResource("PillStroke");

        Animate(Pill, FrameworkElement.WidthProperty, pillW);
        Animate(Pill, FrameworkElement.HeightProperty, pillH);
        ApplyHoverText();
    }

    private Brush SoftAccentStroke()
    {
        Color accent;
        try
        {
            accent = (Color)FindResource("AccentColor");
        }
        catch (ResourceReferenceKeyNotFoundException)
        {
            accent = Color.FromRgb(0xFF, 0x6A, 0x3D);
        }

        return Freeze(new SolidColorBrush(Color.FromArgb(0x70, accent.R, accent.G, accent.B)));
    }

    private static void Animate(UIElement element, DependencyProperty property, double to)
    {
        var from = (double)element.GetValue(property);
        if (double.IsNaN(from))
        {
            element.BeginAnimation(property, null);
            element.SetValue(property, to);
            return;
        }

        if (Math.Abs(from - to) < 0.4)
            return;

        var anim = new DoubleAnimation(from, to, Morph)
        {
            EasingFunction = Ease,
            FillBehavior = FillBehavior.HoldEnd
        };
        element.BeginAnimation(property, anim);
    }

    private void StartGlow()
    {
        if (_glowOn)
            return;
        _glowOn = true;
        var opacity = new DoubleAnimation(0.12, 0.4, TimeSpan.FromMilliseconds(1400))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        GlowHalo.BeginAnimation(UIElement.OpacityProperty, opacity);
    }

    private void StopGlow()
    {
        if (_glowOn)
        {
            _glowOn = false;
            GlowHalo.BeginAnimation(UIElement.OpacityProperty, null);
        }

        if (!_vuGlow)
            GlowHalo.Opacity = 0;
    }

    private void StartDots()
    {
        if (_dotsOn)
            return;
        _dotsOn = true;
        Bounce(Dot0, 0);
        Bounce(Dot1, 70);
        Bounce(Dot2, 140);
        Bounce(Dot3, 210);
        Bounce(Dot4, 280);
    }

    private void StopDots()
    {
        if (!_dotsOn)
            return;
        _dotsOn = false;
        foreach (var dot in new UIElement[] { Dot0, Dot1, Dot2, Dot3, Dot4 })
        {
            dot.BeginAnimation(OpacityProperty, null);
            if (dot.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.YProperty, null);
                t.Y = 0;
            }

            dot.Opacity = 0.35;
        }
    }

    private static void Bounce(UIElement dot, int delayMs)
    {
        var fade = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(380))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        dot.BeginAnimation(UIElement.OpacityProperty, fade);
        if (dot.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            dot.RenderTransform = transform;
        }

        var hop = new DoubleAnimation(2, -3, TimeSpan.FromMilliseconds(380))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        transform.BeginAnimation(TranslateTransform.YProperty, hop);
    }

    private void OnThemeChanged() => Dispatcher.BeginInvoke(ApplyThemePaint);

    private void ApplyThemePaint()
    {
        Color accent;
        Color soft;
        try
        {
            accent = (Color)FindResource("AccentColor");
            soft = (Color)FindResource("AccentSoftColor");
        }
        catch (ResourceReferenceKeyNotFoundException)
        {
            accent = Color.FromRgb(0xFF, 0x6A, 0x3D);
            soft = Color.FromRgb(0xFF, 0xC4, 0xA0);
        }

        _waveFill = Freeze(new SolidColorBrush(accent));
        _waveFillPeak = Freeze(new SolidColorBrush(soft));
        GlowHalo.Fill = new RadialGradientBrush(
            Color.FromArgb(0x88, accent.R, accent.G, accent.B),
            Color.FromArgb(0x00, accent.R, accent.G, accent.B));
        foreach (var bar in _bars)
        {
            if (bar is not null)
                bar.Fill = _waveFill;
        }
    }

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
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (PreviewMode)
        {
            base.OnClosing(e);
            return;
        }

        if (!_forceClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    public void RequestShutdown()
    {
        _forceClose = true;
        System.Windows.Application.Current.Shutdown();
    }
}
