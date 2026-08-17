using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Otoink.App.Asr;
using Otoink.App.Theme;
using Otoink.App.Tray;
using Otoink.App.Win32;
using Otoink.Core;
using Otoink.Core.Ai;
using Otoink.Core.I18n;

namespace Otoink.App;

public partial class App : System.Windows.Application
{
    public SettingsStore SettingsStore { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public TranscriptStore TranscriptStore { get; private set; } = null!;
    public DictationOrchestrator Orchestrator { get; private set; } = null!;
    public UnicodeInjector Injector { get; private set; } = null!;

    private HttpClient? _http;
    private SenseVoiceEngine? _asr;
    private NotifyIconService? _tray;
    private MainWindow? _bar;
    private SettingsWindow? _settingsWindow;
    private TrayMenuWindow? _trayMenu;
    private DispatcherTimer? _foregroundWatch;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            _bar?.ShowToast(args.Exception.Message);
        };

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "otoink",
            "settings.json");

        SettingsStore = new SettingsStore(path);
        Settings = SettingsStore.Load();

        if (string.IsNullOrWhiteSpace(Settings.HoldHotkey))
            Settings.HoldHotkey = "RightCtrl";
        Settings.UiLocale = Loc.Normalize(Settings.UiLocale);
        Loc.Apply(Settings.UiLocale);
        Settings.UiSkin = UiTheme.Normalize(Settings.UiSkin);
        UiTheme.Apply(Settings.UiSkin);

        _http = new HttpClient();
        var ai = new LlmCorrector(
            new OpenAiCompatibleCorrector(_http, () => Settings),
            new AnthropicCorrector(_http, () => Settings),
            () => Settings);
        _asr = new SenseVoiceEngine(() => Settings);
        Injector = new UnicodeInjector();
        TranscriptStore = new TranscriptStore();
        Orchestrator = new DictationOrchestrator(_asr, ai, Injector, TranscriptStore, () => Settings);

        _tray = new NotifyIconService();
        _tray.SettingsRequested += () => Dispatcher.BeginInvoke(ShowSettings);
        _tray.MenuRequested += point => Dispatcher.BeginInvoke(() => ShowTrayMenu(point.X, point.Y));

        var preview = e.Args.Any(a =>
            string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "--preview-export", StringComparison.OrdinalIgnoreCase));
        var export = e.Args.Any(a =>
            string.Equals(a, "--preview-export", StringComparison.OrdinalIgnoreCase));

        if (preview)
        {
            TranscriptStore.Add("喂能听到吗");
            TranscriptStore.Add("喂喂喂喂喂。");
        }

        var window = new MainWindow(Settings, Orchestrator, Injector, CollectIgnoreHwnds, previewMode: preview);
        _bar = window;
        window.OpenSettings = ShowSettings;
        window.TranscriptChanged += () => _settingsWindow?.RefreshHistory();

        if (preview)
        {
            var studio = new Design.DesignStudio(window, ResolveShotDir()) { ExportAndExit = export };
            MainWindow = studio;
            studio.Show();
            window.Show();
            return;
        }

        MainWindow = window;
        window.Show();
        StartForegroundWatch();

        if (ModelLocator.IsInstalled())
        {
            window.BeginWarmup();
            _ = Task.Run(() =>
            {
                try
                {
                    WarmupAsr();
                    window.EndWarmup(success: true);
                }
                catch (Exception ex)
                {
                    window.EndWarmup(success: false, error: ex.Message);
                }
            });
        }
        else
            window.NotifyModelMissing();
    }

    private IEnumerable<IntPtr> CollectIgnoreHwnds()
    {
        if (_trayMenu is not null)
        {
            var hwnd = new WindowInteropHelper(_trayMenu).Handle;
            if (hwnd != IntPtr.Zero)
                yield return hwnd;
        }
    }

    private IEnumerable<IntPtr> CollectOurWindows()
    {
        if (_bar is not null)
        {
            var hwnd = new WindowInteropHelper(_bar).Handle;
            if (hwnd != IntPtr.Zero)
                yield return hwnd;
        }

        if (_settingsWindow is not null)
        {
            var hwnd = _settingsWindow.Hwnd;
            if (hwnd != IntPtr.Zero)
                yield return hwnd;
        }

        if (_trayMenu is not null)
        {
            var hwnd = new WindowInteropHelper(_trayMenu).Handle;
            if (hwnd != IntPtr.Zero)
                yield return hwnd;
        }
    }

    private void StartForegroundWatch()
    {
        _foregroundWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _foregroundWatch.Tick += (_, _) => Injector.RememberExternalForeground(CollectOurWindows());
        _foregroundWatch.Start();
    }

    private void ShowSettings()
    {
        try
        {
            Injector.RememberExternalForeground(CollectOurWindows());
            if (_settingsWindow is null)
            {
                _settingsWindow = new SettingsWindow(
                    SettingsStore, Settings, TranscriptStore, Orchestrator, Injector);
                _settingsWindow.ErrorRaised += message => _bar?.ShowToast(message);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.RefreshHistory();
        }
        catch (Exception ex)
        {
            try
            {
                _settingsWindow?.Close();
            }
            catch
            {
                // Already broken; drop the instance.
            }

            _settingsWindow = null;
            _bar?.ShowToast(ex.Message);
        }
    }

    private void ShowTrayMenu(int pixelX, int pixelY)
    {
        _trayMenu?.Close();
        var menu = new TrayMenuWindow();
        _trayMenu = menu;
        menu.SettingsChosen += ShowSettings;
        menu.ExitChosen += () => _bar?.RequestShutdown();
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenu, menu))
                _trayMenu = null;
        };
        menu.Show();
        menu.PlaceAtScreenPixels(pixelX, pixelY);
    }

    private static string ResolveShotDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Otoink.sln")))
                return Path.Combine(dir.FullName, "design", "shots");
            dir = dir.Parent;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "otoink",
            "shots");
    }

    private void WarmupAsr() => _asr?.Warmup();

    protected override void OnExit(ExitEventArgs e)
    {
        _foregroundWatch?.Stop();
        _foregroundWatch = null;
        _tray?.Dispose();
        _tray = null;
        _asr?.Dispose();
        _http?.Dispose();
        base.OnExit(e);
    }
}
