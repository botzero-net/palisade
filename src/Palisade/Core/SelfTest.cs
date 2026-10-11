using System.Text.Json;

namespace Palisade.Core;

/// <summary>
/// Non-UI smoke test: probes the system and runs detection across the whole
/// catalog without changing anything. Used by CI (Palisade.exe --self-test).
/// </summary>
public static class SelfTest
{
    public static bool Run()
    {
        try
        {
            var snap = SystemProbe.CaptureAsync().GetAwaiter().GetResult();
            var engine = new Engine(Journal.Load());
            var states = new Dictionary<string, string>();
            foreach (var t in engine.Catalog)
                states[t.Id] = engine.DetectTweak(t).ToString();

            var report = new
            {
                version = AppVersion.Text,
                machine = snap.ComputerName,
                os = snap.OsCaption,
                osVersion = snap.OsVersion,
                elevated = snap.Elevated,
                antivirus = snap.AntivirusProducts.Select(p => $"{p.Name} ({p.StateSummary})"),
                defender = new
                {
                    queried = snap.Defender.Queried,
                    antivirusEnabled = snap.Defender.AntivirusEnabled,
                    realTimeProtection = snap.Defender.RealTimeProtection,
                    tamperProtection = snap.Defender.TamperProtection,
                    runningMode = snap.Defender.RunningMode,
                },
                tweaksDetected = states.Count,
                tweakStates = states,
            };
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SELF-TEST FAILED: " + ex);
            return false;
        }
    }
}
