using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NAudio.Wave;
using Otoink.App.Motion;
using Otoink.App.Theme;
using Otoink.App.Win32;
using Otoink.Core;
using Otoink.Core.Ai;
using Otoink.Core.I18n;

namespace Otoink.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly DictationOrchestrator _orchestrator;
    private readonly HistoryPanel _historyPanel;
    private bool _suppressSave;
    private int _tabIndex;

    public event Action<string>? ErrorRaised;

    public SettingsWindow(
        SettingsStore store,
        AppSettings settings,
        TranscriptStore history,
        DictationOrchestrator orchestrator,
        UnicodeInjector injector)
    {
        _store = store;
        _settings = settings;
        _orchestrator = orchestrator;
        InitializeComponent();
        _historyPanel = new HistoryPanel(history, orchestrator, injector);
        _historyPanel.ErrorRaised += message => ErrorRaised?.Invoke(message);
        HistoryPage.Child = _historyPanel;
        Loc.Changed += OnLocChanged;
        UiTheme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            Loc.Changed -= OnLocChanged;
            UiTheme.Changed -= OnThemeChanged;
            StopMeter();
            SaveApiFields();
        };
        BuildSkinSwatches();
        LoadFromSettings();
        ApplyTexts();
        ShowTab(0, animate: false);
    }

    public void RefreshHistory() => _historyPanel.Refresh();

    public IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    private void OnLocChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyTexts();
            _historyPanel.Refresh();
            ShowTab(_tabIndex);
        });
    }

    private void ApplyTexts()
    {
        Title = Loc.T("Settings.Title");
        TabGeneral.Content = Loc.T("Settings.TabGeneral");
        TabAudio.Content = Loc.T("Settings.TabAudio");
        TabApi.Content = Loc.T("Settings.TabApi");
        TabHistory.Content = Loc.T("Settings.TabHistory");
        LanguageLabel.Text = Loc.T("Settings.Language");
        SkinLabel.Text = Loc.T("Settings.Skin");
        AutoPunctuationLabel.Text = Loc.T("Settings.AutoPunctuation");
        HoldHotkeyLabel.Text = Loc.T("Settings.HoldHotkey");
        HoldHotkeyBox.Text = Loc.T("Settings.HoldHotkeyHint");
        AutoPolishLabel.Text = Loc.T("Settings.AutoPolish");
        AutoPolishHint.Text = Loc.T("Settings.AutoPolishHint");
        AudioHint.Text = Loc.T("Settings.AudioHint");
        MicRefreshButton.Content = Loc.T("Settings.MicRefresh");
        MicLevelLabel.Text = Loc.T("Settings.MicLevel");
        MicFormatHint.Text = Loc.T("Settings.MicFormat");
        ApiProviderLabel.Text = Loc.T("Settings.ApiProvider");
        ApiBaseUrlLabel.Text = Loc.T("Settings.ApiBaseUrl");
        ApiKeyLabel.Text = Loc.T("Settings.ApiKey");
        ApiModelLabel.Text = Loc.T("Settings.ApiModel");
        ApiPromptLabel.Text = Loc.T("Settings.ApiPrompt");
        ApiSaveButton.Content = Loc.T("Settings.ApiSave");
        ApiTestButton.Content = Loc.T("Settings.ApiTest");
        ApiRestoreButton.Content = Loc.T("Settings.ApiRestore");
        if (ApiSavedLabel.Opacity < 0.05)
            ApiSavedLabel.Text = Loc.T("Settings.ApiSaved");

        _suppressSave = true;
        try
        {
            LanguageCombo.Items.Clear();
            LanguageCombo.Items.Add(new LocaleOption(Loc.ZhHans, Loc.T("Lang.zhHans")));
            LanguageCombo.Items.Add(new LocaleOption(Loc.En, Loc.T("Lang.en")));
            LanguageCombo.SelectedIndex = Loc.Current == Loc.En ? 1 : 0;
            FillProviderCombo();
            UpdateApiHint();
            foreach (var child in SkinList.Children)
            {
                if (child is RadioButton { Tag: string id } swatch)
                    swatch.Content = Loc.T("Skin." + id);
            }
        }
        finally
        {
            _suppressSave = false;
        }
    }

    private void LoadFromSettings()
    {
        _suppressSave = true;
        try
        {
            DefaultAiInputToggle.IsChecked = _settings.DefaultAiInput;
            AutoPunctuationToggle.IsChecked = _settings.AutoPunctuation;

            ReloadMicrophones();

            FillProviderCombo();
            ApiBaseUrlBox.Text = _settings.ApiBaseUrl;
            ApiKeyBox.Password = _settings.ApiKey;
            ApiModelBox.Text = _settings.ApiModel;
            ApiPromptBox.Text = string.IsNullOrWhiteSpace(_settings.ApiPrompt)
                ? OpenAiCompatibleCorrector.SystemPrompt
                : _settings.ApiPrompt;

            if (string.IsNullOrWhiteSpace(_settings.HoldHotkey))
                _settings.HoldHotkey = "RightCtrl";

            var skin = UiTheme.Normalize(_settings.UiSkin);
            foreach (var child in SkinList.Children)
            {
                if (child is RadioButton { Tag: string id } swatch)
                    swatch.IsChecked = id == skin;
            }
        }
        finally
        {
            _suppressSave = false;
        }
    }

    private void Persist()
    {
        if (_suppressSave)
            return;
        _store.Save(_settings);
    }

    private void ShowTab(int index) => ShowTab(index, animate: true);

    private void ShowTab(int index, bool animate)
    {
        _tabIndex = index;
        GeneralPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        AudioPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ApiPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        if (index == 3)
            _historyPanel.Refresh();
        if (index == 1)
            StartMeter();
        else
            StopMeter();

        TabGeneral.Tag = index == 0 ? "on" : null;
        TabAudio.Tag = index == 1 ? "on" : null;
        TabApi.Tag = index == 2 ? "on" : null;
        TabHistory.Tag = index == 3 ? "on" : null;
        PageTitle.Text = index switch
        {
            1 => Loc.T("Settings.TabAudio"),
            2 => Loc.T("Settings.TabApi"),
            3 => Loc.T("Settings.TabHistory"),
            _ => Loc.T("Settings.TabGeneral")
        };

        if (!animate)
            return;

        FadeIn(index == 0 ? GeneralPage : index == 1 ? AudioPage : index == 2 ? ApiPage : HistoryPage);
        TitleRule.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(10, 28, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = PageEase
        });
    }

    private static readonly IEasingFunction PageEase = CreatePageEase();

    private static IEasingFunction CreatePageEase()
    {
        var ease = new CubicBezierEase(0.16, 1, 0.3, 1);
        ease.Freeze();
        return ease;
    }

    private static void FadeIn(UIElement page)
    {
        if (page.RenderTransform is not TranslateTransform shift)
        {
            shift = new TranslateTransform();
            page.RenderTransform = shift;
        }

        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = PageEase
        });
        page.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = PageEase
        });
    }

    private void OnThemeChanged() => Dispatcher.BeginInvoke(PaintAtmosphere);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PaintAtmosphere();
    }

    private void PaintAtmosphere()
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

        Heat.Background = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(1.02, 1.1),
            GradientOrigin = new Point(1.02, 1.1),
            RadiusX = 0.95,
            RadiusY = 1.12,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x38, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(0x12, accent.R, accent.G, accent.B), 0.42),
                new GradientStop(Colors.Transparent, 0.88)
            }
        };
        ShellStroke.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x55, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(0x3A, 0xE7, 0xE9, 0xEA), 0.38),
                new GradientStop(Color.FromArgb(0x28, 0xE7, 0xE9, 0xEA), 0.72),
                new GradientStop(Color.FromArgb(0x18, accent.R, accent.G, accent.B), 1)
            }
        };
    }

    private void OnTabGeneral(object sender, RoutedEventArgs e) => ShowTab(0);
    private void OnTabAudio(object sender, RoutedEventArgs e) => ShowTab(1);
    private void OnTabApi(object sender, RoutedEventArgs e) => ShowTab(2);
    private void OnTabHistory(object sender, RoutedEventArgs e) => ShowTab(3);

    private void BuildSkinSwatches()
    {
        SkinList.Children.Clear();
        foreach (var id in UiTheme.All)
        {
            var swatch = new RadioButton
            {
                GroupName = "otoink-skin",
                Style = (Style)FindResource("SkinSwatch"),
                Tag = id,
                Background = new SolidColorBrush(UiTheme.AccentOf(id)),
                Content = Loc.T("Skin." + id)
            };
            swatch.Checked += OnSkinChecked;
            SkinList.Children.Add(swatch);
        }
    }

    private void OnSkinChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSave)
            return;
        if (sender is not RadioButton { Tag: string id })
            return;
        _settings.UiSkin = id;
        Persist();
        UiTheme.Apply(id);
    }

    private void OnChromeDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSave)
            return;
        if (LanguageCombo.SelectedItem is not LocaleOption option)
            return;
        _settings.UiLocale = option.Id;
        Persist();
        Loc.Apply(option.Id);
    }

    private void OnDefaultAiInputChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSave)
            return;
        _settings.DefaultAiInput = DefaultAiInputToggle.IsChecked == true;
        Persist();
    }

    private void OnAutoPunctuationChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSave)
            return;
        _settings.AutoPunctuation = AutoPunctuationToggle.IsChecked == true;
        Persist();
    }

    private void OnMicrophoneSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSave)
            return;
        if (MicrophoneCombo.SelectedItem is MicrophoneOption option)
        {
            _settings.MicrophoneId = option.DeviceNumber.ToString();
            Persist();
            if (_tabIndex == 1)
                StartMeter();
        }
    }

    private bool _apiTestBusy;

    private void OnApiSaveClick(object sender, RoutedEventArgs e)
    {
        SaveApiFields();
        ShowApiStatus(Loc.T("Settings.ApiSaved"), ok: true);
    }

    private async void OnApiTestClick(object sender, RoutedEventArgs e)
    {
        if (_apiTestBusy)
            return;

        SaveApiFields();
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            ShowApiStatus(Loc.T("Settings.ApiTestNoKey"), ok: false);
            return;
        }

        _apiTestBusy = true;
        ApiTestButton.IsEnabled = false;
        ApiSaveButton.IsEnabled = false;
        ApiTestButton.Content = Loc.T("Settings.ApiTesting");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await _orchestrator.ProbeApiAsync(cts.Token);
            ShowApiStatus(Loc.T("Settings.ApiTestOk"), ok: true);
        }
        catch (OperationCanceledException)
        {
            ShowApiStatus(Loc.T("Settings.ApiTestTimeout"), ok: false);
        }
        catch (Exception ex)
        {
            ShowApiStatus(Loc.Format("Settings.ApiTestFailDetail", ShortError(ex.Message)), ok: false);
        }
        finally
        {
            _apiTestBusy = false;
            ApiTestButton.IsEnabled = true;
            ApiSaveButton.IsEnabled = true;
            ApiTestButton.Content = Loc.T("Settings.ApiTest");
        }
    }

    private void OnApiRestorePromptClick(object sender, RoutedEventArgs e)
    {
        ApiPromptBox.Text = OpenAiCompatibleCorrector.SystemPrompt;
    }

    private static string ShortError(string message)
    {
        var text = message.Trim();
        if (text.StartsWith("API Key is not set", StringComparison.Ordinal))
            return Loc.T("Settings.ApiTestNoKey");
        return text.Length <= 140 ? text : text[..140];
    }

    private void ShowApiStatus(string text, bool ok)
    {
        ApiSavedLabel.BeginAnimation(UIElement.OpacityProperty, null);
        ApiSavedLabel.Text = text;
        ApiSavedLabel.Foreground = ok
            ? (Brush)FindResource("Accent")
            : new SolidColorBrush(Color.FromRgb(0xE8, 0x8B, 0x7A));
        ApiSavedLabel.Opacity = 1;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900))
        {
            BeginTime = TimeSpan.FromMilliseconds(ok ? 900 : 2400),
            EasingFunction = PageEase
        };
        ApiSavedLabel.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void SaveApiFields()
    {
        _settings.ApiBaseUrl = ApiBaseUrlBox.Text.Trim();
        _settings.ApiKey = ApiKeyBox.Password;
        _settings.ApiModel = ApiModelBox.Text.Trim();
        _settings.ApiProvider = ApiProviderCombo.SelectedItem is ProviderOption provider
            ? provider.Id
            : ApiProvider.OpenAiCompatible;
        var prompt = ApiPromptBox.Text.Trim();
        _settings.ApiPrompt = prompt == OpenAiCompatibleCorrector.SystemPrompt
            ? ""
            : prompt;
        Persist();
    }

    private WaveInEvent? _meter;

    private void FillProviderCombo()
    {
        var current = ApiProvider.Normalize(_settings.ApiProvider);
        ApiProviderCombo.Items.Clear();
        ApiProviderCombo.Items.Add(new ProviderOption(ApiProvider.OpenAiCompatible, Loc.T("Settings.ApiProviderOpenAi")));
        ApiProviderCombo.Items.Add(new ProviderOption(ApiProvider.Anthropic, Loc.T("Settings.ApiProviderAnthropic")));
        ApiProviderCombo.SelectedIndex = current == ApiProvider.Anthropic ? 1 : 0;
    }

    private void UpdateApiHint()
    {
        var id = ApiProviderCombo.SelectedItem is ProviderOption option
            ? option.Id
            : ApiProvider.Normalize(_settings.ApiProvider);
        ApiHint.Text = ApiProvider.IsAnthropic(id)
            ? Loc.T("Settings.ApiHintAnthropic")
            : Loc.T("Settings.ApiHintOpenAi");
    }

    private void OnApiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSave)
            return;
        if (ApiProviderCombo.SelectedItem is not ProviderOption option)
            return;

        var previous = ApiProvider.Normalize(_settings.ApiProvider);
        _settings.ApiProvider = option.Id;
        if (previous != option.Id)
        {
            if (ApiProvider.IsStockUrl(ApiBaseUrlBox.Text, previous))
            {
                ApiBaseUrlBox.Text = ApiProvider.IsAnthropic(option.Id)
                    ? ApiProvider.AnthropicDefaultUrl
                    : ApiProvider.OpenAiDefaultUrl;
            }

            if (ApiProvider.IsStockModel(ApiModelBox.Text, previous))
            {
                ApiModelBox.Text = ApiProvider.IsAnthropic(option.Id)
                    ? ApiProvider.AnthropicDefaultModel
                    : ApiProvider.OpenAiDefaultModel;
            }
        }

        UpdateApiHint();
    }

    private void ReloadMicrophones()
    {
        MicrophoneCombo.Items.Clear();
        try
        {
            var deviceCount = WaveIn.DeviceCount;
            var selectedIndex = -1;
            for (var i = 0; i < deviceCount; i++)
            {
                var name = WaveIn.GetCapabilities(i).ProductName;
                MicrophoneCombo.Items.Add(new MicrophoneOption(i, name));
                if (_settings.MicrophoneId == i.ToString())
                    selectedIndex = i;
            }

            if (selectedIndex >= 0)
                MicrophoneCombo.SelectedIndex = selectedIndex;
            else if (deviceCount > 0 && string.IsNullOrEmpty(_settings.MicrophoneId))
                MicrophoneCombo.SelectedIndex = 0;
        }
        catch (Exception)
        {
            // NAudio can fail on some devices; keep the page open.
        }
    }

    private void OnMicRefreshClick(object sender, RoutedEventArgs e)
    {
        ReloadMicrophones();
        if (_tabIndex == 1)
            StartMeter();
    }

    private void StartMeter()
    {
        StopMeter();
        MicFormatHint.Text = Loc.T("Settings.MicFormat");
        if (MicrophoneCombo.SelectedItem is not MicrophoneOption option)
            return;

        try
        {
            var meter = new WaveInEvent
            {
                DeviceNumber = option.DeviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 40
            };
            meter.DataAvailable += OnMeterData;
            meter.StartRecording();
            _meter = meter;
        }
        catch
        {
            MicFormatHint.Text = Loc.T("Settings.MicBusy");
        }
    }

    private void StopMeter()
    {
        if (_meter is null)
            return;
        _meter.DataAvailable -= OnMeterData;
        try
        {
            _meter.StopRecording();
        }
        catch
        {
            // ignore teardown
        }

        _meter.Dispose();
        _meter = null;
        MicLevelFill.Width = 0;
    }

    private void OnMeterData(object? sender, WaveInEventArgs e)
    {
        var peak = 0f;
        var limit = e.BytesRecorded - 1;
        for (var i = 0; i < limit; i += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
            if (sample > peak)
                peak = sample;
        }

        Dispatcher.BeginInvoke(() =>
        {
            var track = MicLevelTrack.ActualWidth;
            if (track < 1)
                return;
            MicLevelFill.Width = track * Math.Clamp(peak * 2.2, 0, 1);
        });
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        var dark = 1;
        if (NativeMethods.DwmSetWindowAttribute(
                hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(
                hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
        }
    }

    private sealed record ProviderOption(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record LocaleOption(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record MicrophoneOption(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }
}
