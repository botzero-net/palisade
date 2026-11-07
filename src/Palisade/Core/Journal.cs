using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Palisade.Core;

public sealed class JournalEntry
{
    public DateTimeOffset Time { get; set; }
    public string SessionId { get; set; } = "";
    public string TweakId { get; set; } = "";
    public string TweakTitle { get; set; } = "";
    public int ActionIndex { get; set; }
    public string ActionKind { get; set; } = "";
    public string Description { get; set; } = "";
    public string UndoJson { get; set; } = "";
    public bool Reverted { get; set; }
    public string? RevertedAt { get; set; }
}

public sealed class JournalSession
{
    public string SessionId { get; init; } = "";
    public DateTimeOffset Started { get; init; }
    public int ChangeCount { get; init; }
    public int RevertedCount { get; set; }
}

/// <summary>
/// Append-only JSON journal of every state change Palisade makes, stored at
/// %ProgramData%\Palisade\journal.json. This is what powers rollback:
/// each entry carries an <see cref="ActionUndo"/> captured before the change.
/// </summary>
public sealed class Journal
{
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Palisade");

    public static string JournalPath => Path.Combine(DataDir, "journal.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    private readonly List<JournalEntry> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<JournalEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public static Journal Load()
    {
        EnsureSecureDataDir();
        var j = new Journal();
        try
        {
            if (File.Exists(JournalPath))
            {
                var entries = JsonSerializer.Deserialize<List<JournalEntry>>(File.ReadAllText(JournalPath));
                if (entries != null) j._entries.AddRange(entries);
            }
        }
        catch
        {
            // A corrupt journal should never block the app; rollback just has nothing to show.
        }
        return j;
    }

    public void Record(string sessionId, Tweak tweak, int actionIndex, TweakAction action, ActionUndo undo)
    {
        lock (_lock)
        {
            _entries.Add(new JournalEntry
            {
                Time = DateTimeOffset.Now,
                SessionId = sessionId,
                TweakId = tweak.Id,
                TweakTitle = tweak.Title,
                ActionIndex = actionIndex,
                ActionKind = undo.Kind,
                Description = action.Describe(),
                UndoJson = undo.ToJson(),
            });
            Save();
        }
    }

    public void MarkReverted(JournalEntry entry)
    {
        lock (_lock)
        {
            entry.Reverted = true;
            entry.RevertedAt = DateTimeOffset.Now.ToString("o");
            Save();
        }
    }

    /// <summary>Applied (not yet reverted) sessions, newest first.</summary>
    public List<JournalSession> Sessions()
    {
        return Entries
            .Where(e => !e.Reverted)
            .GroupBy(e => e.SessionId)
            .Select(g => new JournalSession
            {
                SessionId = g.Key,
                Started = g.Min(e => e.Time),
                ChangeCount = g.Count(),
                RevertedCount = 0,
            })
            .OrderByDescending(s => s.Started)
            .ToList();
    }

    public List<JournalEntry> ActiveEntriesFor(string sessionId) =>
        Entries.Where(e => e.SessionId == sessionId && !e.Reverted)
               .OrderByDescending(e => e.Time)
               .ToList();

    private static bool _aclEnsured;

    /// <summary>
    /// Creates the data dir and restricts it to Administrators + SYSTEM.
    /// Journal values are substituted into elevated PowerShell during rollback,
    /// so a standard user must not be able to write (or pre-create) them.
    /// </summary>
    public static void EnsureSecureDataDir()
    {
        Directory.CreateDirectory(DataDir);
        if (_aclEnsured) return;
        _aclEnsured = true;
        try
        {
            var dir = new DirectoryInfo(DataDir);
            var acl = dir.GetAccessControl();
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            AddAdminRule(acl, "S-1-5-32-544"); // Administrators
            AddAdminRule(acl, "S-1-5-18");     // SYSTEM
            dir.SetAccessControl(acl);

            if (File.Exists(JournalPath))
            {
                var file = new FileInfo(JournalPath);
                var facl = new FileSecurity();
                facl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                AddAdminRule(facl, "S-1-5-32-544");
                AddAdminRule(facl, "S-1-5-18");
                file.SetAccessControl(facl);
            }
        }
        catch
        {
            // Best effort — a weaker ACL should never break the journal itself.
        }
    }

    private static void AddAdminRule(FileSystemSecurity acl, string sid) =>
        acl.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(sid),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

    private void Save()
    {
        EnsureSecureDataDir();
        var tmp = JournalPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, JsonOpts));
        File.Move(tmp, JournalPath, overwrite: true);
    }
}
