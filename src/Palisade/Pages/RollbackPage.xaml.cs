using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Palisade.Core;

namespace Palisade.Pages;

public sealed class SessionRow
{
    public required string SessionId { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public string Detail { get; set; } = "";
}

public partial class RollbackPage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;

    public RollbackPage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
    }

    public string StepTitle => "Rollback";
    public UIElement View => this;
    public string NextText => "";
    public bool ShowBack => false;
    public bool CanContinue => true;
    public event EventHandler? StateChanged { add { } remove { } }

    public void OnShown() => Refresh();

    private void Refresh()
    {
        var sessions = _ctx.Journal.Sessions();
        var rows = sessions.Select(s =>
        {
            var entries = _ctx.Journal.ActiveEntriesFor(s.SessionId);
            var names = entries.Select(e => e.TweakTitle).Distinct().Take(4).ToList();
            var more = entries.Select(e => e.TweakTitle).Distinct().Count() - names.Count;
            return new SessionRow
            {
                SessionId = s.SessionId,
                Title = $"Session {s.SessionId} — {s.Started.LocalDateTime:MMM d, yyyy · h:mm tt}",
                Subtitle = $"{s.ChangeCount} change{(s.ChangeCount == 1 ? "" : "s")} still active",
                Detail = string.Join(" · ", names) + (more > 0 ? $" · +{more} more" : ""),
            };
        }).ToList();

        Sessions.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RollbackAllButton.IsEnabled = rows.Count > 0;
    }

    private async void RollbackSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sessionId }) return;
        var answer = MessageBox.Show(
            "Restore every setting this session changed to its previous value?",
            "Roll back session", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        StatusText.Text = "Rolling back…";
        var (reverted, failed, errors) = await _ctx.Engine.RollbackSessionAsync(sessionId);
        StatusText.Text = failed == 0
            ? $"Rolled back {reverted} change{(reverted == 1 ? "" : "s")}."
            : $"Rolled back {reverted}, {failed} failed: {string.Join("; ", errors)}";
        Refresh();
    }

    private async void RollbackAll_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Restore every setting Palisade has ever changed on this machine?",
            "Roll back everything", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        StatusText.Text = "Rolling back…";
        var total = 0; var failedTotal = 0; var allErrors = new List<string>();
        foreach (var s in _ctx.Journal.Sessions())
        {
            var (reverted, failed, errors) = await _ctx.Engine.RollbackSessionAsync(s.SessionId);
            total += reverted; failedTotal += failed; allErrors.AddRange(errors);
        }
        StatusText.Text = failedTotal == 0
            ? $"Rolled back {total} change{(total == 1 ? "" : "s")}."
            : $"Rolled back {total}, {failedTotal} failed: {string.Join("; ", allErrors)}";
        Refresh();
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start("explorer.exe", Journal.DataDir); } catch { }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _ctx.GoToStep?.Invoke(0);
}
