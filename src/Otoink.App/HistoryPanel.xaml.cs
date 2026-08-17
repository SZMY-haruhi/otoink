using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Otoink.App.Win32;
using Otoink.Core;
using Otoink.Core.I18n;

namespace Otoink.App;

public partial class HistoryPanel : UserControl
{
    private readonly TranscriptStore _store;
    private readonly DictationOrchestrator _orchestrator;
    private readonly UnicodeInjector _injector;
    private readonly ObservableCollection<HistoryRowVm> _rows = new();

    public event Action<string>? ErrorRaised;

    public HistoryPanel(TranscriptStore store, DictationOrchestrator orchestrator, UnicodeInjector injector)
    {
        _store = store;
        _orchestrator = orchestrator;
        _injector = injector;
        InitializeComponent();
        HistoryList.ItemsSource = _rows;
        Refresh();
        Loc.Changed += OnLocChanged;
        Unloaded += (_, _) => Loc.Changed -= OnLocChanged;
    }

    private void OnLocChanged() => Dispatcher.BeginInvoke(Refresh);

    public void Refresh()
    {
        var optimizingIds = _rows.Where(r => r.IsOptimizing).Select(r => r.Id).ToHashSet();

        _rows.Clear();
        foreach (var entry in _store.ListNewestFirst())
        {
            var row = new HistoryRowVm(entry);
            if (optimizingIds.Contains(entry.Id))
                row.IsOptimizing = true;
            _rows.Add(row);
        }

        EmptyTitle.Text = Loc.T("History.EmptyTitle");
        EmptyBody.Text = Loc.T("History.EmptyBody");
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistoryRowVm row })
            return;
        if (row.IsOptimizing)
            return;

        var id = row.Id;
        row.IsOptimizing = true;

        try
        {
            var updated = await _orchestrator.OptimizeAsync(id, CancellationToken.None);
            if (FindRow(id) is { } current)
                current.Apply(updated);
        }
        catch (Exception ex)
        {
            ErrorRaised?.Invoke(ex.Message);
        }
        finally
        {
            if (FindRow(id) is { } current)
                current.IsOptimizing = false;
        }
    }

    private void OnInsertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistoryRowVm row })
            return;
        if (!_injector.TryFocusLastApp())
        {
            ErrorRaised?.Invoke(Loc.T("Toast.FocusFirst"));
            return;
        }

        _orchestrator.Insert(row.Id);
        row.NotifyInserted();
    }

    private HistoryRowVm? FindRow(Guid id) =>
        _rows.FirstOrDefault(r => r.Id == id);

    private sealed class HistoryRowVm : INotifyPropertyChanged
    {
        private string _rawText = "";
        private string? _correctedText;
        private bool _isOptimizing;
        private bool _inserted;
        private int _insertToken;

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
            CorrectedText is null ? "" : Loc.T("History.AiPrefix") + CorrectedText;

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
            }
        }

        public bool CanOptimize => !IsOptimizing;

        public string OptimizeButtonText => IsOptimizing ? Loc.T("History.Optimizing") : Loc.T("History.Optimize");

        public string InsertButtonText => _inserted ? Loc.T("History.Inserted") : Loc.T("History.Insert");

        public async void NotifyInserted()
        {
            var token = ++_insertToken;
            _inserted = true;
            OnPropertyChanged(nameof(InsertButtonText));
            await Task.Delay(1200);
            if (token != _insertToken)
                return;
            _inserted = false;
            OnPropertyChanged(nameof(InsertButtonText));
        }

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
