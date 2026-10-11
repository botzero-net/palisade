using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Palisade.Core;

namespace Palisade.Pages;

public partial class CheckupPage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;
    private bool _loaded;

    public CheckupPage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
    }

    public string StepTitle => "Checkup";
    public UIElement View => this;
    public string NextText => "Choose protections →";
    public bool ShowBack => true;
    public bool CanContinue => _loaded;
    public event EventHandler? StateChanged;
    private void Changed() => StateChanged?.Invoke(this, EventArgs.Empty);

    public async void OnShown()
    {
        if (_loaded) return;
        _ctx.Snapshot ??= await SystemProbe.CaptureAsync();
        var s = _ctx.Snapshot;
        _loaded = true;

        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Visibility.Visible;

        OsText.Text = $"{(string.IsNullOrWhiteSpace(s.OsCaption) ? "Windows" : s.OsCaption)} · build {s.OsVersion}";
        ElevatedText.Text = s.Elevated
            ? "Running with administrator rights ✓"
            : "Not elevated — most protections will fail";

        var bl = s.SystemDriveEncryption;
        BitLockerText.Text = !bl.Queried
            ? "Unknown (BitLocker may not exist on this edition)"
            : bl.Protection == "On"
                ? $"BitLocker on · {bl.VolumeStatus} ✓"
                : $"Off · {bl.VolumeStatus}. Consider enabling device encryption in Settings.";

        AvList.ItemsSource = s.AntivirusProducts;

        var d = s.Defender;
        if (!d.Queried)
        {
            DefenderCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            SetStat(RtpText, d.RealTimeProtection, "On", "Off");
            SetStat(TamperText, d.TamperProtection, "On", "Off");
            SigText.Text = d.SignaturesUpToDate ? "Up to date" : "Out of date";
            SigText.Foreground = Brush(d.SignaturesUpToDate);
        }

        var anyAvOn = s.AntivirusProducts.Any(p => p.StateSummary.Contains("on"));
        var defenderActive = d.Queried && d.AntivirusEnabled;
        NoAvBanner.Visibility = (anyAvOn || defenderActive) ? Visibility.Collapsed : Visibility.Visible;
        TamperBanner.Visibility = d.Queried && defenderActive && !d.TamperProtection
            ? Visibility.Visible : Visibility.Collapsed;

        Changed();
    }

    private static SolidColorBrush Brush(bool good) =>
        (SolidColorBrush)System.Windows.Application.Current.Resources[good ? "GoodBrush" : "BadBrush"];

    private static void SetStat(TextBlock tb, bool on, string onText, string offText)
    {
        tb.Text = on ? onText : offText;
        tb.Foreground = Brush(on);
    }
}
