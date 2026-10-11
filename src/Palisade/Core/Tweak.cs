namespace Palisade.Core;

/// <summary>A single hardening choice shown to the user.</summary>
public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    /// <summary>One-line summary shown next to the checkbox.</summary>
    public required string Description { get; init; }
    /// <summary>Longer explanation shown under "What this changes".</summary>
    public string? Details { get; init; }
    public bool Recommended { get; init; } = true;
    public bool RequiresReboot { get; init; }
    public required IReadOnlyList<TweakAction> Actions { get; init; }
}
