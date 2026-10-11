using System.Runtime.InteropServices;
using System.Windows;
using Palisade.Core;

namespace Palisade;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);
    private const uint AttachParentProcess = 0xFFFFFFFF;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--self-test"))
        {
            AttachConsole(AttachParentProcess);
            Environment.ExitCode = SelfTest.Run() ? 0 : 1;
            Shutdown();
            return;
        }

        if (e.Args.Contains("--list"))
        {
            AttachConsole(AttachParentProcess);
            foreach (var t in Tweaks.Catalog)
                Console.WriteLine($"{t.Id}\t{t.Category}\t{t.Title}");
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }
}
