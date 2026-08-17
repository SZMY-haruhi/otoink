using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Otoink.Core.I18n;

namespace Otoink.App.Tray;

public partial class TrayMenuWindow : Window
{
    private readonly DispatcherTimer _life;
    private bool _armed;

    public TrayMenuWindow()
    {
        InitializeComponent();
        ApplyTexts();
        Loc.Changed += ApplyTexts;

        _life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _life.Tick += (_, _) =>
        {
            _life.Stop();
            if (IsVisible)
                Close();
        };

        Closed += (_, _) =>
        {
            Loc.Changed -= ApplyTexts;
            _life.Stop();
        };

        Loaded += async (_, _) =>
        {
            _life.Start();
            await Task.Delay(280);
            if (!IsVisible)
                return;
            Activate();
            _armed = true;
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        MouseEnter += (_, _) => _life.Stop();
        MouseLeave += (_, _) =>
        {
            _life.Interval = TimeSpan.FromSeconds(1.2);
            _life.Stop();
            _life.Start();
        };
    }

    public event Action? SettingsChosen;
    public event Action? ExitChosen;

    public void PlaceAtScreenPixels(int pixelX, int pixelY)
    {
        void Place()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var x = pixelX / dpi.DpiScaleX;
            var y = pixelY / dpi.DpiScaleY;
            Left = x;
            Top = y - Math.Max(ActualHeight, 80);
        }

        if (IsLoaded)
            Place();
        else
            Loaded += (_, _) => Place();
    }

    private void ApplyTexts()
    {
        SettingsItem.Content = Loc.T("Tray.Settings");
        ExitItem.Content = Loc.T("Tray.Exit");
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        SettingsChosen?.Invoke();
        Close();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        ExitChosen?.Invoke();
        Close();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_armed && IsVisible)
            Close();
    }
}
