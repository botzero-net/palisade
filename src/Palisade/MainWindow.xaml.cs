using System.Collections.ObjectModel;
using System.Windows;
using Palisade.Core;
using Palisade.Pages;

namespace Palisade;

public partial class MainWindow : Window
{
    private readonly WizardContext _ctx = new();
    private readonly List<IWizardPage> _steps;
    private readonly ObservableCollection<StepItem> _railItems = new();
    private IWizardPage? _specialPage;
    private int _index;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Text}";

        _steps = new List<IWizardPage>
        {
            new WelcomePage(_ctx),
            new CheckupPage(_ctx),
            new ChoicesPage(_ctx),
            new ApplyPage(_ctx),
            new ResultsPage(_ctx),
        };

        for (var i = 0; i < _steps.Count; i++)
        {
            _railItems.Add(new StepItem { Number = i + 1, Title = _steps[i].StepTitle });
            var page = _steps[i];
            page.StateChanged += (_, _) => UpdateFooter();
        }
        StepsList.ItemsSource = _railItems;

        _ctx.GoToStep = GoTo;
        _ctx.ShowRollback = ShowRollback;

        GoTo(0);
    }

    private void GoTo(int index)
    {
        _index = index;
        _specialPage = null;
        var page = _steps[index];
        PageHost.Content = page.View;
        UpdateRail();
        UpdateFooter();
        page.OnShown();
    }

    private void ShowRollback()
    {
        _specialPage ??= new RollbackPage(_ctx);
        PageHost.Content = _specialPage.View;
        foreach (var s in _railItems) s.IsCurrent = false;
        StepsList.Items.Refresh();
        UpdateFooter();
        _specialPage.OnShown();
    }

    private void UpdateRail()
    {
        for (var i = 0; i < _railItems.Count; i++)
        {
            _railItems[i].IsCurrent = i == _index;
            _railItems[i].IsDone = i < _index;
        }
        StepsList.Items.Refresh();
    }

    private void UpdateFooter()
    {
        if (_specialPage != null)
        {
            Footer.Visibility = Visibility.Collapsed;
            return;
        }
        Footer.Visibility = Visibility.Visible;
        var page = _steps[_index];
        NextButton.Content = page.NextText;
        NextButton.IsEnabled = page.CanContinue;
        BackButton.Visibility = page.ShowBack && _index > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_index >= _steps.Count - 1)
        {
            Close();
            return;
        }
        GoTo(_index + 1);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0) GoTo(_index - 1);
    }

    private void RollbackNav_Click(object sender, RoutedEventArgs e) => ShowRollback();
}
