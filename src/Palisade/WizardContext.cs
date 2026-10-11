using System.Windows;
using Palisade.Core;

namespace Palisade;

/// <summary>Shared state carried across wizard pages.</summary>
public sealed class WizardContext
{
    public Journal Journal { get; } = Journal.Load();
    public Engine Engine { get; }
    public SystemSnapshot? Snapshot { get; set; }
    public List<Tweak> SelectedTweaks { get; } = new();
    public ApplyOutcome? Outcome { get; set; }

    /// <summary>Navigate to a wizard step (set by MainWindow).</summary>
    public Action<int>? GoToStep { get; set; }
    /// <summary>Open the rollback page (set by MainWindow).</summary>
    public Action? ShowRollback { get; set; }
    /// <summary>Step to return to when leaving the rollback page (set by MainWindow).</summary>
    public int ReturnStep { get; set; }

    public WizardContext() => Engine = new Engine(Journal);
}

/// <summary>One step of the wizard.</summary>
public interface IWizardPage
{
    string StepTitle { get; }
    UIElement View { get; }
    string NextText { get; }
    bool ShowBack { get; }
    bool CanContinue { get; }
    void OnShown();
    /// <summary>Raised when CanContinue/NextText may have changed.</summary>
    event EventHandler? StateChanged;
}

public sealed class StepItem
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public bool IsCurrent { get; set; }
    public bool IsDone { get; set; }
}
