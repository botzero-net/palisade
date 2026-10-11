using System.IO;

namespace Palisade.Core;

public enum ApplyItemStatus
{
    Pending,
    Running,
    Applied,
    AlreadySet,
    Skipped,
    Failed,
}

public sealed class ApplyProgress
{
    public required string TweakId { get; init; }
    public required string Title { get; init; }
    public int Index { get; init; }
    public int Total { get; init; }
    public ApplyItemStatus Status { get; set; }
    public string? Message { get; set; }
}

public sealed class ApplyOutcome
{
    public int Applied { get; set; }
    public int AlreadySet { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool RebootSuggested { get; set; }
    public string SessionId { get; set; } = "";
    public string? LogPath { get; set; }
}

/// <summary>Detects, applies and rolls back tweaks; writes the journal and text log.</summary>
public sealed class Engine
{
    private readonly Journal _journal;
    private StreamWriter? _log;

    public Engine(Journal journal) => _journal = journal;

    public IReadOnlyList<Tweak> Catalog => Tweaks.Catalog;

    public TweakState DetectTweak(Tweak tweak)
    {
        var states = tweak.Actions.Select(SafeDetect).ToList();
        if (states.All(s => s == TweakState.NotApplicable)) return TweakState.NotApplicable;
        if (states.Any(s => s == TweakState.NotApplied)) return TweakState.NotApplied;
        if (states.All(s => s is TweakState.Applied or TweakState.NotApplicable)) return TweakState.Applied;
        if (states.All(s => s == TweakState.Applied)) return TweakState.Applied;
        return TweakState.Unknown;
    }

    private static TweakState SafeDetect(TweakAction action)
    {
        try { return action.Detect(); }
        catch { return TweakState.Unknown; }
    }

    public async Task<ApplyOutcome> ApplyAsync(
        IReadOnlyList<Tweak> selected,
        IProgress<ApplyProgress> progress,
        CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var outcome = new ApplyOutcome { SessionId = sessionId };
        var logPath = Path.Combine(Journal.DataDir, $"palisade-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        outcome.LogPath = logPath;
        Directory.CreateDirectory(Journal.DataDir);

        await Task.Run(() =>
        {
            using (_log = new StreamWriter(logPath, append: true) { AutoFlush = true })
            {
                Log($"Palisade {AppVersion.Text} — apply session {sessionId} started {DateTime.Now:u}");
                Log($"OS: {Environment.OSVersion}; Machine: {Environment.MachineName}; User: {Environment.UserName}");

                for (var i = 0; i < selected.Count; i++)
                {
                    var tweak = selected[i];
                    var state = new ApplyProgress
                    {
                        TweakId = tweak.Id, Title = tweak.Title,
                        Index = i, Total = selected.Count,
                        Status = ApplyItemStatus.Running,
                    };
                    progress.Report(state);
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var appliedAny = false;
                        var allNa = true;
                        var alreadyAll = true;

                        for (var a = 0; a < tweak.Actions.Count; a++)
                        {
                            var action = tweak.Actions[a];
                            var st = SafeDetect(action);
                            if (st != TweakState.NotApplicable) allNa = false;
                            if (st == TweakState.Applied) continue;
                            alreadyAll = false;
                            if (st == TweakState.NotApplicable) continue;

                            Log($"  [{tweak.Id}] {action.Describe()}");
                            var undo = action.Apply();
                            _journal.Record(sessionId, tweak, a, action, undo);
                            appliedAny = true;
                        }

                        if (allNa)
                        {
                            state.Status = ApplyItemStatus.Skipped;
                            state.Message = "Not applicable on this device";
                            outcome.Skipped++;
                            Log($"  [{tweak.Id}] skipped — not applicable");
                        }
                        else if (!appliedAny && alreadyAll)
                        {
                            state.Status = ApplyItemStatus.AlreadySet;
                            outcome.AlreadySet++;
                            Log($"  [{tweak.Id}] already set");
                        }
                        else
                        {
                            state.Status = ApplyItemStatus.Applied;
                            outcome.Applied++;
                            if (tweak.RequiresReboot) outcome.RebootSuggested = true;
                            Log($"  [{tweak.Id}] applied");
                        }
                    }
                    catch (Exception ex)
                    {
                        state.Status = ApplyItemStatus.Failed;
                        state.Message = ex.Message;
                        outcome.Failed++;
                        Log($"  [{tweak.Id}] FAILED: {ex}");
                    }
                    progress.Report(state);
                }

                Log($"Session {sessionId} finished: {outcome.Applied} applied, " +
                    $"{outcome.AlreadySet} already set, {outcome.Skipped} skipped, {outcome.Failed} failed.");
            }
        }, ct);

        return outcome;
    }

    /// <summary>Roll back all active entries of one apply session, newest first.</summary>
    public async Task<(int reverted, int failed, List<string> errors)> RollbackSessionAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            var entries = _journal.ActiveEntriesFor(sessionId);
            var reverted = 0;
            var failed = 0;
            var errors = new List<string>();
            var tweaks = Catalog.ToDictionary(t => t.Id);

            foreach (var e in entries)
            {
                try
                {
                    var tweak = tweaks[e.TweakId];
                    var action = tweak.Actions[e.ActionIndex];
                    action.Revert(ActionUndo.FromJson(e.UndoJson));
                    _journal.MarkReverted(e);
                    reverted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{e.TweakTitle}: {ex.Message}");
                }
            }
            return (reverted, failed, errors);
        });
    }

    private void Log(string line) => _log?.WriteLine($"{DateTime.Now:HH:mm:ss} {line}");
}

public static class AppVersion
{
    public static string Text =>
        typeof(Engine).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
