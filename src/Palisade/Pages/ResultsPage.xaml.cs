using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Palisade.Core;

namespace Palisade.Pages;

public partial class ResultsPage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;

    public ResultsPage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
    }

    public string StepTitle => "Done";
    public UIElement View => this;
    public string NextText => "Finish";
    public bool ShowBack => false;
    public bool CanContinue => true;
    public event EventHandler? StateChanged { add { } remove { } }

    public void OnShown()
    {
        var o = _ctx.Outcome;
        if (o == null) return;
        AppliedCount.Text = o.Applied.ToString();
        AlreadyCount.Text = o.AlreadySet.ToString();
        SkippedCount.Text = o.Skipped.ToString();
        FailedCount.Text = o.Failed.ToString();
        RebootBanner.Visibility = o.RebootSuggested ? Visibility.Visible : Visibility.Collapsed;
        LogPathText.Text = o.LogPath is { } p ? $"Log: {p}" : "";
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start("explorer.exe", Journal.DataDir); } catch { }
    }

    private void Rollback_Click(object sender, RoutedEventArgs e) => _ctx.ShowRollback?.Invoke();
}
