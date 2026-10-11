using System.Security.Principal;

namespace Palisade.Core;

public sealed class AntivirusProductInfo
{
    public string Name { get; set; } = "";
    public string StateSummary { get; set; } = "";
}

public sealed class DefenderStatus
{
    public bool Queried { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtection { get; set; }
    public bool TamperProtection { get; set; }
    public bool SignaturesUpToDate { get; set; }
    public string SignatureVersion { get; set; } = "";
    public string RunningMode { get; set; } = "";
}

public sealed class BitLockerStatus
{
    public bool Queried { get; set; }
    public string Protection { get; set; } = "";
    public string VolumeStatus { get; set; } = "";
}

public sealed class SystemSnapshot
{
    public string OsCaption { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public bool Elevated { get; set; }
    public List<AntivirusProductInfo> AntivirusProducts { get; set; } = new();
    public DefenderStatus Defender { get; set; } = new();
    public BitLockerStatus SystemDriveEncryption { get; set; } = new();
    public string ComputerName { get; set; } = "";
}

/// <summary>Read-only system inspection shown on the checkup page.</summary>
public static class SystemProbe
{
    public static bool IsElevated()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public static Task<SystemSnapshot> CaptureAsync() => Task.Run(() =>
    {
        var snap = new SystemSnapshot
        {
            Elevated = IsElevated(),
            ComputerName = Environment.MachineName,
            OsVersion = Environment.OSVersion.Version.ToString(),
        };

        try
        {
            var r = PowerShellRunner.Run(
                "(Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Caption)");
            if (r.Success) snap.OsCaption = r.StdOut.Trim();
        }
        catch { }

        snap.AntivirusProducts = GetAntivirusProducts();
        snap.Defender = GetDefenderStatus();
        snap.SystemDriveEncryption = GetBitLockerStatus();
        return snap;
    });

    private static List<AntivirusProductInfo> GetAntivirusProducts()
    {
        var list = new List<AntivirusProductInfo>();
        try
        {
            var r = PowerShellRunner.Run(
                "Get-CimInstance -Namespace 'root/SecurityCenter2' -ClassName AntiVirusProduct | " +
                "ForEach-Object { \"$($_.displayName)|$($_.productState)\" }");
            if (!r.Success) return list;
            foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|');
                var stateHex = parts.Length > 1 ? parts[1] : "";
                var enabled = "";
                try
                {
                    var state = Convert.ToInt32(stateHex.Trim(), 10);
                    enabled = (state & 0x1000) != 0 ? "on" : "off";
                }
                catch { }
                list.Add(new AntivirusProductInfo
                {
                    Name = parts[0],
                    StateSummary = enabled == "" ? "registered" : $"real-time {enabled}",
                });
            }
        }
        catch { }
        return list;
    }

    private static DefenderStatus GetDefenderStatus()
    {
        var st = new DefenderStatus();
        try
        {
            var r = PowerShellRunner.Run(
                "$s = Get-MpComputerStatus; " +
                "\"AntivirusEnabled=$($s.AntivirusEnabled)\";" +
                "\"RealTimeProtectionEnabled=$($s.RealTimeProtectionEnabled)\";" +
                "\"IsTamperProtected=$($s.IsTamperProtected)\";" +
                "\"SigOutOfDate=$($s.AntivirusSignatureAge -gt 7)\";" +
                "\"SignatureVersion=$($s.AntivirusSignatureVersion)\";" +
                "\"RunningMode=$($s.AMRunningMode)\"");
            if (!r.Success) return st;
            st.Queried = true;
            foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var k = line[..eq];
                var v = line[(eq + 1)..];
                var isTrue = v.Equals("True", StringComparison.OrdinalIgnoreCase);
                switch (k)
                {
                    case "AntivirusEnabled": st.AntivirusEnabled = isTrue; break;
                    case "RealTimeProtectionEnabled": st.RealTimeProtection = isTrue; break;
                    case "IsTamperProtected": st.TamperProtection = isTrue; break;
                    case "SigOutOfDate": st.SignaturesUpToDate = !isTrue; break;
                    case "SignatureVersion": st.SignatureVersion = v; break;
                    case "RunningMode": st.RunningMode = v; break;
                }
            }
        }
        catch { }
        return st;
    }

    private static BitLockerStatus GetBitLockerStatus()
    {
        var st = new BitLockerStatus();
        try
        {
            var r = PowerShellRunner.Run(
                "$v = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction Stop; " +
                "\"Protection=$($v.ProtectionStatus)\"; \"Status=$($v.VolumeStatus)\"");
            if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut)) return st;
            st.Queried = true;
            foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (line[..eq] == "Protection") st.Protection = line[(eq + 1)..];
                if (line[..eq] == "Status") st.VolumeStatus = line[(eq + 1)..];
            }
        }
        catch { }
        return st;
    }
}
