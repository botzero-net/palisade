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

    /// <summary>Best-effort plain-language explanation for why a run failed.</summary>
    public static string Reason(PowerShellResult r)
    {
        var s = (r.StdErr ?? "").Trim();
        if (s.Length == 0) return $"exit code {r.ExitCode}";
        if (s == "Timed out") return "timed out before completing";
        if (s.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("Access Denied", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("UnauthorizedAccess", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("0x80070005", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("0x80041003", StringComparison.OrdinalIgnoreCase))
            return "access denied — needs administrator rights, or the setting is protected by Tamper Protection";
        if (s.Contains("not recognized", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("Cannot find", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            return "command or feature not available on this system";
        return s;
    }
}
