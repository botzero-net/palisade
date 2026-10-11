using System.Windows;
using System.Windows.Controls;

namespace Palisade.Pages;

public partial class WelcomePage : UserControl, IWizardPage
{
    private readonly WizardContext _ctx;

    public WelcomePage(WizardContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
    }

    public string StepTitle => "Welcome";
    public UIElement View => this;
    public string NextText => "Get started →";
    public bool ShowBack => false;
    public bool CanContinue => true;
    public void OnShown() { }
    public event EventHandler? StateChanged { add { } remove { } }
}
