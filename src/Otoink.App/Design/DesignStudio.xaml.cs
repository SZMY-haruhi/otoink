using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Otoink.App.Design;

public partial class DesignStudio : Window
{
    private readonly MainWindow _bar;
    private readonly DispatcherTimer _measure;
    private readonly string _shotDir;

    public DesignStudio(MainWindow bar, string shotDir)
    {
        _bar = bar;
        _shotDir = shotDir;
        InitializeComponent();
        Left = 40;
        Top = 80;
        _measure = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _measure.Tick += (_, _) => SizeText.Text = _bar.PreviewSizeText();
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _measure.Stop();
            if (System.Windows.Application.Current.MainWindow == this)
                System.Windows.Application.Current.Shutdown();
        };
    }

    public bool ExportAndExit { get; set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _bar.Left = Left + Width + 28;
        _bar.Top = SystemParameters.WorkArea.Bottom - 120;
        _measure.Start();
        SizeText.Text = _bar.PreviewSizeText();

        if (!ExportAndExit)
            return;

        try
        {
            await ExportAllAsync();
            ExportText.Text = "exported " + _shotDir;
        }
        catch (Exception ex)
        {
            ExportText.Text = ex.Message;
        }

        await Task.Delay(400);
        System.Windows.Application.Current.Shutdown();
    }

    private void OnIdle(object sender, RoutedEventArgs e) => _bar.PreviewIdle();

    private void OnHold(object sender, RoutedEventArgs e) => _bar.PreviewHold();

    private void OnToggle(object sender, RoutedEventArgs e) => _bar.PreviewToggle();

    private void OnProcessing(object sender, RoutedEventArgs e) => _bar.PreviewProcessing();

    private void OnToast(object sender, RoutedEventArgs e) => _bar.PreviewToast();

    private void OnSettings(object sender, RoutedEventArgs e) => _bar.OpenSettings?.Invoke();

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await CaptureCurrentAsync("current.png");
            ExportText.Text = path;
        }
        catch (Exception ex)
        {
            ExportText.Text = ex.Message;
        }
    }

    public async Task ExportAllAsync()
    {
        Directory.CreateDirectory(_shotDir);
        _bar.PreviewIdle();
        await WaitVisualAsync();
        await CaptureCurrentAsync("01-idle.png");

        _bar.PreviewHold();
        await WaitVisualAsync();
        await CaptureCurrentAsync("02-recording.png");

        _bar.PreviewToggle();
        await WaitVisualAsync();
        await CaptureCurrentAsync("03-click-to-talk.png");

        _bar.PreviewProcessing();
        await WaitVisualAsync();
        await CaptureCurrentAsync("04-processing.png");

        _bar.PreviewToast();
        await WaitVisualAsync();
        await CaptureCurrentAsync("05-toast.png");

        _bar.OpenSettings?.Invoke();
        await WaitVisualAsync();
        await CaptureCurrentAsync("06-settings.png");

        _bar.PreviewIdle();
        await WaitVisualAsync();
    }

    private async Task<string> CaptureCurrentAsync(string fileName)
    {
        await WaitVisualAsync();
        var path = Path.Combine(_shotDir, fileName);
        var hwnds = _bar.PreviewCaptureHwnds().ToList();
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is not SettingsWindow)
                continue;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
                hwnds.Add(hwnd);
        }

        return WindowCapture.Save(hwnds, path);
    }

    private static async Task WaitVisualAsync()
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(180);
    }
}
