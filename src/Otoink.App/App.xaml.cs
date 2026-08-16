using System.IO;
using System.Net.Http;
using System.Windows;
using Otoink.App.Asr;
using Otoink.App.Win32;
using Otoink.Core;
using Otoink.Core.Ai;

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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "otoink",
            "settings.json");

        SettingsStore = new SettingsStore(path);
        Settings = SettingsStore.Load();

        if (string.IsNullOrWhiteSpace(Settings.HoldHotkey))
            Settings.HoldHotkey = "RightCtrl";

        _http = new HttpClient();
        var ai = new OpenAiCompatibleCorrector(_http, () => Settings);
        _asr = new SenseVoiceEngine(() => Settings);
        Injector = new UnicodeInjector();
        TranscriptStore = new TranscriptStore();
        Orchestrator = new DictationOrchestrator(_asr, ai, Injector, TranscriptStore, () => Settings);

        var window = new MainWindow(SettingsStore, Settings, TranscriptStore, Orchestrator, Injector);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _asr?.Dispose();
        _http?.Dispose();
        base.OnExit(e);
    }
}
