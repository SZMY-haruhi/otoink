using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Otoink.Core;

namespace Otoink.App;

public partial class HistoryPanel : UserControl
{
    private readonly TranscriptStore _store;
    private readonly DictationOrchestrator _orchestrator;
    private readonly ObservableCollection<HistoryRowVm> _rows = new();

    public HistoryPanel(TranscriptStore store, DictationOrchestrator orchestrator)
    {
        _store = store;
        _orchestrator = orchestrator;
        InitializeComponent();
        HistoryList.ItemsSource = _rows;
        Refresh();
    }

    public void Refresh()
    {
        var optimizingIds = _rows.Where(r => r.IsOptimizing).Select(r => r.Id).ToHashSet();
        var errors = _rows
            .Where(r => !string.IsNullOrEmpty(r.ErrorMessage) && !r.IsOptimizing)
            .ToDictionary(r => r.Id, r => r.ErrorMessage!);

        _rows.Clear();
        foreach (var entry in _store.ListNewestFirst())
        {
            var row = new HistoryRowVm(entry);
            if (optimizingIds.Contains(entry.Id))
                row.IsOptimizing = true;
            else if (errors.TryGetValue(entry.Id, out var err))
                row.ErrorMessage = err;
            _rows.Add(row);
        }
    }

    private async void OnOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistoryRowVm row })
            return;
        if (row.IsOptimizing)
            return;

        row.IsOptimizing = true;
        row.ErrorMessage = null;

        try
        {
            var updated = await _orchestrator.OptimizeAsync(row.Id, CancellationToken.None);
            row.Apply(updated);
            row.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            row.ErrorMessage = ex.Message;
        }
        finally
        {
            row.IsOptimizing = false;
        }
    }

    private void OnInsertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistoryRowVm row })
            return;
        _orchestrator.Insert(row.Id);
    }

    private sealed class HistoryRowVm : INotifyPropertyChanged
    {
        private string _rawText = "";
        private string? _correctedText;
        private bool _isOptimizing;
        private string? _errorMessage;

        public HistoryRowVm(TranscriptEntry entry) => Apply(entry);

        public Guid Id { get; private set; }

        public string RawText
        {
            get => _rawText;
            private set => SetField(ref _rawText, value);
        }

        public string? CorrectedText
        {
            get => _correctedText;
            private set
            {
                if (!SetField(ref _correctedText, value))
                    return;
                OnPropertyChanged(nameof(CorrectedDisplay));
                OnPropertyChanged(nameof(CorrectedVisibility));
            }
        }

        public string CorrectedDisplay =>
            CorrectedText is null ? "" : "AI：" + CorrectedText;

        public Visibility CorrectedVisibility =>
            CorrectedText is null ? Visibility.Collapsed : Visibility.Visible;

        public bool IsOptimizing
        {
            get => _isOptimizing;
            set
            {
                if (!SetField(ref _isOptimizing, value))
                    return;
                OnPropertyChanged(nameof(CanOptimize));
                OnPropertyChanged(nameof(OptimizeButtonText));
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (!SetField(ref _errorMessage, value))
                    return;
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }

        public Visibility ErrorVisibility =>
            !IsOptimizing && !string.IsNullOrEmpty(ErrorMessage)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public bool CanOptimize => !IsOptimizing;

        public string OptimizeButtonText => IsOptimizing ? "优化中…" : "AI 优化";

        public void Apply(TranscriptEntry entry)
        {
            Id = entry.Id;
            RawText = entry.RawText;
            CorrectedText = entry.CorrectedText;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
