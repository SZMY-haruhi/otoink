using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;
using Otoink.Core;

namespace Otoink.App;

public partial class SettingsFlyout : UserControl
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private bool _suppressSave;

    public SettingsFlyout(SettingsStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
        InitializeComponent();
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _suppressSave = true;
        try
        {
            DefaultAiInputToggle.IsChecked = _settings.DefaultAiInput;
            AutoPunctuationToggle.IsChecked = _settings.AutoPunctuation;

            MicrophoneCombo.Items.Clear();
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

            ApiBaseUrlBox.Text = _settings.ApiBaseUrl;
            ApiKeyBox.Password = _settings.ApiKey;
            ApiModelBox.Text = _settings.ApiModel;

            if (string.IsNullOrWhiteSpace(_settings.HoldHotkey))
                _settings.HoldHotkey = "RightCtrl";
            HoldHotkeyBox.Text = _settings.HoldHotkey;
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
        }
    }

    private void OnApiBaseUrlLostFocus(object sender, RoutedEventArgs e) => SaveApiTextFields();

    private void OnApiModelLostFocus(object sender, RoutedEventArgs e) => SaveApiTextFields();

    private void OnApiFieldTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSave)
            return;
        SaveApiTextFields();
    }

    private void SaveApiTextFields()
    {
        if (_suppressSave)
            return;
        _settings.ApiBaseUrl = ApiBaseUrlBox.Text;
        _settings.ApiModel = ApiModelBox.Text;
        Persist();
    }

    private void OnApiKeyLostFocus(object sender, RoutedEventArgs e) => SaveApiKey();

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSave)
            return;
        SaveApiKey();
    }

    private void SaveApiKey()
    {
        if (_suppressSave)
            return;
        _settings.ApiKey = ApiKeyBox.Password;
        Persist();
    }

    private sealed record MicrophoneOption(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }
}
