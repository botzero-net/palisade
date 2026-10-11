using System.Diagnostics;
using System.Text;

namespace Palisade.Core;

public sealed class PowerShellResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
}

/// <summary>Runs short Windows PowerShell snippets and captures their output.</summary>
public static class PowerShellRunner
{
    public static PowerShellResult Run(string script, int timeoutMs = 60000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);

            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                return new PowerShellResult { Success = false, ExitCode = -1, StdErr = "Timed out" };
            }
            return new PowerShellResult
            {
                Success = p.ExitCode == 0,
                ExitCode = p.ExitCode,
                StdOut = stdout.Result,
                StdErr = stderr.Result,
            };
        }
        catch (Exception ex)
        {
            return new PowerShellResult { Success = false, ExitCode = -1, StdErr = ex.Message };
        }
    }
}
