using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace Palisade.Core;

public enum TweakState
{
    Unknown,
    Applied,
    NotApplied,
    NotApplicable,
    Failed,
}

/// <summary>Everything needed to restore a single action to its prior state.</summary>
public sealed class ActionUndo
{
    public string Kind { get; set; } = "";
    public Dictionary<string, string?> Data { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this);
    public static ActionUndo FromJson(string json) =>
        JsonSerializer.Deserialize<ActionUndo>(json) ?? new ActionUndo();
}

public abstract class TweakAction
{
    public abstract string Kind { get; }

    /// <summary>Check whether the desired state is already in effect.</summary>
    public abstract TweakState Detect();

    /// <summary>Apply the change and return a record that can undo it.</summary>
    public abstract ActionUndo Apply();

    /// <summary>Restore the state captured by <see cref="Apply"/>.</summary>
    public abstract void Revert(ActionUndo undo);

    /// <summary>Human-readable description of what this action changes.</summary>
    public abstract string Describe();
}

/// <summary>Sets a registry value (the permanent equivalent of a local/group policy).</summary>
public sealed class RegistryTweakAction : TweakAction
{
    public required RegistryHive Hive { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required object DesiredValue { get; init; }
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    public override string Kind => "registry";

    public override string Describe()
    {
        var hive = Hive switch
        {
            RegistryHive.LocalMachine => "HKLM",
            RegistryHive.CurrentUser => "HKCU",
            RegistryHive.Users => "HKU",
            _ => Hive.ToString(),
        };
        return $"{hive}\\{Path} → {Name} = {FormatValue()}";
    }

    private string FormatValue() =>
        DesiredValue is int i && ValueKind == RegistryValueKind.DWord
            ? $"{i} (DWORD)"
            : $"\"{DesiredValue}\"";

    private RegistryKey Open(bool writable)
    {
        var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
        var baseKey = RegistryKey.OpenBaseKey(Hive, view);
        return writable
            ? baseKey.CreateSubKey(Path, writable: true)
            : baseKey.OpenSubKey(Path, writable: false)!;
    }

    public override TweakState Detect()
    {
        try
        {
            using var key = Open(writable: false);
            if (key == null) return TweakState.NotApplied;
            var current = key.GetValue(Name);
            if (current == null) return TweakState.NotApplied;
            return ValuesEqual(current) ? TweakState.Applied : TweakState.NotApplied;
        }
        catch
        {
            return TweakState.Unknown;
        }
    }

    private bool ValuesEqual(object current)
    {
        if (ValueKind == RegistryValueKind.DWord)
            return current is int i && i == Convert.ToInt32(DesiredValue);
        return string.Equals(current.ToString(), DesiredValue.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public override ActionUndo Apply()
    {
        var undo = new ActionUndo { Kind = Kind };
        undo.Data["hive"] = Hive.ToString();
        undo.Data["path"] = Path;
        undo.Data["name"] = Name;

        using var key = Open(writable: true);
        var prior = key.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (prior == null)
        {
            undo.Data["existed"] = "false";
        }
        else
        {
            undo.Data["existed"] = "true";
            undo.Data["priorKind"] = key.GetValueKind(Name).ToString();
            undo.Data["priorValue"] = prior is string[] arr ? string.Join("\n", arr) : prior.ToString();
        }

        key.SetValue(Name, DesiredValue, ValueKind);
        return undo;
    }

    public override void Revert(ActionUndo undo)
    {
        var path = undo.Data["path"]!;
        var name = undo.Data["name"]!;
        var hive = Enum.Parse<RegistryHive>(undo.Data["hive"]!);
        var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(path, writable: true);
        if (key == null) return;

        if (undo.Data["existed"] != "true")
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        else
        {
            var priorKind = Enum.Parse<RegistryValueKind>(undo.Data["priorKind"]!);
            var priorText = undo.Data["priorValue"] ?? "";
            object value = priorKind switch
            {
                RegistryValueKind.DWord => int.Parse(priorText),
                RegistryValueKind.QWord => long.Parse(priorText),
                RegistryValueKind.MultiString => priorText.Split('\n'),
                _ => priorText,
            };
            key.SetValue(name, value, priorKind);
        }
    }
}

/// <summary>
/// Sets a service startup type via its registry key (permanent, survives reboots)
/// and optionally stops it immediately.
/// </summary>
public sealed class ServiceStartupAction : TweakAction
{
    public required string ServiceName { get; init; }
    public required int DesiredStart { get; init; } // 2 auto, 3 manual, 4 disabled
    public bool StopNow { get; init; } = true;

    private const string ServicesRoot = @"SYSTEM\CurrentControlSet\Services";

    public override string Kind => "service";

    public override string Describe()
    {
        var label = DesiredStart switch { 2 => "Automatic", 3 => "Manual", 4 => "Disabled", _ => DesiredStart.ToString() };
        return $"Service {ServiceName} → start type {label}" + (StopNow && DesiredStart == 4 ? " and stop now" : "");
    }

    public override TweakState Detect()
    {
        var reg = new RegistryTweakAction
        {
            Hive = RegistryHive.LocalMachine,
            Path = $@"{ServicesRoot}\{ServiceName}",
            Name = "Start",
            DesiredValue = DesiredStart,
            ValueKind = RegistryValueKind.DWord,
        };
        return reg.Detect();
    }

    public override ActionUndo Apply()
    {
        var reg = new RegistryTweakAction
        {
            Hive = RegistryHive.LocalMachine,
            Path = $@"{ServicesRoot}\{ServiceName}",
            Name = "Start",
            DesiredValue = DesiredStart,
            ValueKind = RegistryValueKind.DWord,
        };
        var undo = reg.Apply();
        undo.Kind = Kind;
        undo.Data["service"] = ServiceName;
        undo.Data["stopNow"] = StopNow ? "true" : "false";

        if (StopNow && DesiredStart == 4)
            Run("sc.exe", $"stop \"{ServiceName}\"", ignoreErrors: true);

        return undo;
    }

    public override void Revert(ActionUndo undo)
    {
        var reg = new RegistryTweakAction
        {
            Hive = RegistryHive.LocalMachine,
            Path = $@"{ServicesRoot}\{undo.Data["service"]}",
            Name = "Start",
            DesiredValue = 0,
        };
        reg.Revert(new ActionUndo { Kind = "registry", Data = undo.Data });

        // If the service was auto/manual before, try to start it again.
        if (undo.Data["existed"] == "true" && undo.Data["priorValue"] is "2" or "3")
            Run("sc.exe", $"start \"{undo.Data["service"]}\"", ignoreErrors: true);
    }

    private static void Run(string exe, string args, bool ignoreErrors)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(15000);
        }
        catch when (ignoreErrors) { }
    }
}

/// <summary>Runs a Windows PowerShell command; undo restores the captured prior state.</summary>
public sealed class PowerShellAction : TweakAction
{
    /// <summary>Script that prints "true" when the desired state is already set.</summary>
    public required string DetectScript { get; init; }
    /// <summary>Script that prints "key=value" lines capturing prior state, then applies the change.</summary>
    public required string ApplyScript { get; init; }
    /// <summary>Template for revert; {key} placeholders are filled from captured prior state.</summary>
    public required string RevertScriptTemplate { get; init; }
    public required string Description { get; init; }

    public override string Kind => "powershell";
    public override string Describe() => Description;

    public override TweakState Detect()
    {
        var r = PowerShellRunner.Run(DetectScript);
        if (!r.Success) return TweakState.Unknown;
        var v = r.StdOut.Trim();
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return TweakState.Applied;
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return TweakState.NotApplied;
        if (v.Equals("na", StringComparison.OrdinalIgnoreCase)) return TweakState.NotApplicable;
        return TweakState.Unknown;
    }

    public override ActionUndo Apply()
    {
        var r = PowerShellRunner.Run(ApplyScript);
        if (!r.Success)
            throw new InvalidOperationException($"PowerShell action failed: {r.StdErr.Trim()}");

        var undo = new ActionUndo { Kind = Kind };
        foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = line.IndexOf('=');
            if (eq > 0 && line.StartsWith("prior_", StringComparison.Ordinal))
                undo.Data[line[..eq]] = line[(eq + 1)..];
        }
        return undo;
    }

    public override void Revert(ActionUndo undo)
    {
        var script = RevertScriptTemplate;
        foreach (var kv in undo.Data)
            script = script.Replace("{" + kv.Key + "}", kv.Value ?? "");
        PowerShellRunner.Run(script);
    }
}

/// <summary>Enables or disables a Windows optional feature via DISM (e.g. SMBv1, Recall).</summary>
public sealed class OptionalFeatureAction : TweakAction
{
    public required string FeatureName { get; init; }
    public required bool DesiredEnabled { get; init; }

    public override string Kind => "feature";
    public override string Describe() =>
        $"Windows feature {FeatureName} → {(DesiredEnabled ? "enabled" : "disabled")}";

    public override TweakState Detect()
    {
        var r = PowerShellRunner.Run(
            $"$f = Get-WindowsOptionalFeature -Online -FeatureName '{FeatureName}' -ErrorAction SilentlyContinue; " +
            "if ($null -eq $f) { 'na' } elseif ($f.State -eq 'Enabled') { 'true' } else { 'false' }");
        if (!r.Success) return TweakState.Unknown;
        return r.StdOut.Trim() switch
        {
            "na" => TweakState.NotApplicable,
            "true" => TweakState.Applied,
            "false" => TweakState.NotApplied,
            _ => TweakState.Unknown,
        };
    }

    public override ActionUndo Apply()
    {
        var state = Detect();
        var undo = new ActionUndo { Kind = Kind };
        undo.Data["feature"] = FeatureName;
        undo.Data["prior"] = state switch
        {
            TweakState.Applied => "enabled",
            TweakState.NotApplied => "disabled",
            _ => "unknown",
        };
        if (state == TweakState.NotApplicable) return undo;

        // DISM component-store changes can exceed a minute on a cold store (e.g.
        // Recall on Copilot+ PCs) — allow five minutes and retry once on timeout.
        var script = (DesiredEnabled ? "Enable" : "Disable") +
            $"-WindowsOptionalFeature -Online -FeatureName '{FeatureName}' -NoRestart -ErrorAction Stop | Out-Null; 'ok'";
        var r = PowerShellRunner.Run(script, timeoutMs: 300000);
        if (!r.Success && r.StdErr.Trim() == "Timed out")
            r = PowerShellRunner.Run(script, timeoutMs: 300000);
        if (!r.Success)
            throw new InvalidOperationException($"DISM feature change failed for {FeatureName}: {r.StdErr.Trim()}");
        return undo;
    }

    public override void Revert(ActionUndo undo)
    {
        var prior = undo.Data["prior"];
        if (prior == "enabled")
            PowerShellRunner.Run($"Enable-WindowsOptionalFeature -Online -FeatureName '{FeatureName}' -NoRestart | Out-Null", timeoutMs: 300000);
        else if (prior == "disabled")
            PowerShellRunner.Run($"Disable-WindowsOptionalFeature -Online -FeatureName '{FeatureName}' -NoRestart | Out-Null", timeoutMs: 300000);
    }
}

/// <summary>A simple command line whose revert is a fixed counter-command.</summary>
public sealed class CommandAction : TweakAction
{
    public required string DetectCommand { get; init; }   // exit 0 = applied, 1 = not applied
    public required string ApplyCommand { get; init; }
    public required string RevertCommand { get; init; }
    public required string Description { get; init; }

    public override string Kind => "command";
    public override string Describe() => Description;

    private static int RunCmd(string command)
    {
        var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(20000);
        return p.ExitCode;
    }

    public override TweakState Detect() =>
        RunCmd(DetectCommand) == 0 ? TweakState.Applied : TweakState.NotApplied;

    public override ActionUndo Apply()
    {
        var prior = Detect();
        var code = RunCmd(ApplyCommand);
        if (code != 0) throw new InvalidOperationException($"Command failed ({code}): {ApplyCommand}");
        var undo = new ActionUndo { Kind = Kind };
        undo.Data["appliedPrior"] = prior == TweakState.Applied ? "true" : "false";
        return undo;
    }

    public override void Revert(ActionUndo undo)
    {
        if (undo.Data["appliedPrior"] != "true")
            RunCmd(RevertCommand);
    }
}
