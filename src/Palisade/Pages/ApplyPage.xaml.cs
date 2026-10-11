using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Palisade.Core;

namespace Palisade.Pages;

public sealed class ApplyRowVM : INotifyPropertyChanged
{
    public required string Title { get; init; }
    private string _glyph = "…";
    private Brush _glyphBrush = Brushes.Gray;
    private string _statusText = "waiting";
    private string? _detail;

    public string Glyph { get => _glyph; set { _glyph = value; Ch(nameof(Glyph)); } }
    public Brush GlyphBrush { get => _glyphBrush; set { _glyphBrush = value; Ch(nameof(GlyphBrush)); } }
    public string StatusText { get => _statusText; set { _statusText = value; Ch(nameof(StatusText)); } }
    public string? Detail
    {
        get => _detail;
        set { _detail = value; Ch(nameof(Detail)); Ch(nameof(DetailVisibility)); }
    }
    public Visibility DetailVisibility =>
        string.IsNullOrEmpty(_detail) ? Visibility.Collapsed : Visibility.Visible;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Ch(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class ApplyPage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;
    private readonly ObservableCollection<ApplyRowVM> _rows = new();
    private readonly Dictionary<string, ApplyRowVM> _byId = new();
    private bool _ran;

    public ApplyPage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        Rows.ItemsSource = _rows;
    }

    public string StepTitle => "Apply";
    public UIElement View => this;
    public string NextText => _ran ? "See results →" : "Working…";
    public bool ShowBack => false;
    public bool CanContinue => _ran;
    public event EventHandler? StateChanged;
    private void Changed() => StateChanged?.Invoke(this, EventArgs.Empty);

    public async void OnShown()
    {
        if (_ran) return;
        var tweaks = _ctx.SelectedTweaks;
        SubtitleText.Text =
            $"Applying {tweaks.Count} protection{(tweaks.Count == 1 ? "" : "s")} — every change is being journaled for rollback.";

        _rows.Clear();
        _byId.Clear();
        foreach (var t in tweaks)
        {
            var row = new ApplyRowVM { Title = t.Title };
            _rows.Add(row);
            _byId[t.Id] = row;
        }

        var progress = new Progress<ApplyProgress>(p =>
        {
            if (!_byId.TryGetValue(p.TweakId, out var row)) return;
            Progress.Value = 100.0 * (p.Index + 1) / tweaks.Count;
            switch (p.Status)
            {
                case ApplyItemStatus.Running:
                    row.Glyph = "…"; row.StatusText = "applying";
                    row.GlyphBrush = Res("AccentBrush");
                    break;
                case ApplyItemStatus.Applied:
                    row.Glyph = "✓"; row.StatusText = "applied";
                    row.GlyphBrush = Res("GoodBrush");
                    break;
                case ApplyItemStatus.AlreadySet:
                    row.Glyph = "✓"; row.StatusText = "already on";
                    row.GlyphBrush = Res("GoodBrush");
                    break;
                case ApplyItemStatus.Skipped:
                    row.Glyph = "—"; row.StatusText = "not applicable";
                    row.GlyphBrush = Res("SubtleTextBrush");
                    break;
                case ApplyItemStatus.Failed:
                    row.Glyph = "✗"; row.StatusText = "failed";
                    row.GlyphBrush = Res("BadBrush");
                    row.Detail = p.Message;
                    break;
            }
        });

        try
        {
            _ctx.Outcome = await _ctx.Engine.ApplyAsync(tweaks, progress);
        }
        catch (Exception ex)
        {
            _ctx.Outcome = new ApplyOutcome { Failed = tweaks.Count };
            foreach (var r in _rows) { r.Glyph = "✗"; r.StatusText = "failed"; r.Detail = ex.Message; }
        }

        _ran = true;
        Progress.Value = 100;
        SubtitleText.Text = "Done — see the summary on the next screen.";
        Changed();
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
