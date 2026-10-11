using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Palisade.Core;

namespace Palisade.Pages;

public sealed class TweakOption : INotifyPropertyChanged
{
    public Tweak Tweak { get; }
    private bool _isSelected;
    private TweakState _state = TweakState.Unknown;

    public TweakOption(Tweak tweak)
    {
        Tweak = tweak;
        _isSelected = tweak.Recommended;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnChanged(nameof(IsSelected)); }
    }

    public TweakState State
    {
        get => _state;
        set
        {
            _state = value;
            OnChanged(nameof(StateText));
            OnChanged(nameof(StateBrush));
        }
    }

    public string StateText => _state switch
    {
        TweakState.Applied => "already on",
        TweakState.NotApplied => "off",
        TweakState.NotApplicable => "n/a on this device",
        TweakState.Unknown => "checking…",
        _ => "",
    };

    public Brush StateBrush
    {
        get
        {
            var key = _state switch
            {
                TweakState.Applied => "GoodBrush",
                TweakState.NotApplied => "SubtleTextBrush",
                TweakState.NotApplicable => "WarnBrush",
                _ => "SubtleTextBrush",
            };
            return (SolidColorBrush)Application.Current.Resources[key];
        }
    }

    public Visibility RecommendedBadgeVisibility =>
        Tweak.Recommended ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RebootBadgeVisibility =>
        Tweak.RequiresReboot ? Visibility.Visible : Visibility.Collapsed;

    public IEnumerable<string> ActionDescriptions => Tweak.Actions.Select(a => a.Describe());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class ChoicesPage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;
    private readonly List<TweakOption> _options;
    private bool _detected;

    public ChoicesPage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _options = _ctx.Engine.Catalog.Select(t => new TweakOption(t)).ToList();
        foreach (var o in _options)
            o.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TweakOption.IsSelected)) UpdateCount();
            };

        var view = CollectionViewSource.GetDefaultView(_options);
        view.GroupDescriptions.Add(new PropertyGroupDescription("Tweak.Category"));
        ItemsList.ItemsSource = view;
        UpdateCount();
    }

    public string StepTitle => "Protections";
    public UIElement View => this;
    public string NextText => "Apply protections →";
    public bool ShowBack => true;
    public bool CanContinue => _options.Any(o => o.IsSelected);
    public event EventHandler? StateChanged;
    private void Changed() => StateChanged?.Invoke(this, EventArgs.Empty);

    public async void OnShown()
    {
        if (_detected) return;
        _detected = true;
        DetectingText.Text = "Checking current state…";

        var engine = _ctx.Engine;
        await Task.WhenAll(_options.Select(o => Task.Run(() =>
        {
            o.State = engine.DetectTweak(o.Tweak);
        })));

        DetectingText.Text = "";
        UpdateCount();
    }

    private void UpdateCount()
    {
        var sel = _options.Where(o => o.IsSelected).ToList();
        CountText.Text = $"{sel.Count} of {_options.Count} selected";
        _ctx.SelectedTweaks.Clear();
        _ctx.SelectedTweaks.AddRange(sel.Select(o => o.Tweak));
        Changed();
    }

    private void SelectRecommended_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in _options) o.IsSelected = o.Tweak.Recommended;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in _options) o.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in _options) o.IsSelected = false;
    }
}
