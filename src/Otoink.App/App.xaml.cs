using System.IO;
using System.Windows;
using Otoink.Core;

namespace Otoink.App;

public partial class App : System.Windows.Application
{
    public SettingsStore SettingsStore { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;

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

        var window = new MainWindow(SettingsStore, Settings);
        MainWindow = window;
        window.Show();
    }
}
