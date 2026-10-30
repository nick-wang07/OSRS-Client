namespace Core.Input;

/// <summary>
/// Randomized delay ranges applied around input events. Every range is inclusive of the
/// minimum and exclusive of the maximum.
/// </summary>
/// <remarks>
/// These cover only micro-timing. Session-level pacing (breaks, fatigue, reaction time)
/// is a separate concern and is not modeled here.
/// </remarks>
public sealed record InputTimings
{
    public static InputTimings Default { get; } = new();

    /// <summary>All delays removed. Intended for tests so they do not sleep.</summary>
    public static InputTimings Instant { get; } = new()
    {
        ClickHoldMs = (0, 1),
        BeforeClickMs = (0, 1),
        ModifierMs = (0, 1),
        KeyPressMs = (0, 1),
        ScrollNotchMs = (0, 1),
        DragSettleMs = (0, 1),
        KeystrokeMs = (0, 1),
        MoveStepDelayMs = 0,
        MoveStepsPerDelay = 1,
    };

    public (int Min, int Max) ClickHoldMs { get; init; } = (60, 120);

    public (int Min, int Max) BeforeClickMs { get; init; } = (20, 50);

    public (int Min, int Max) ModifierMs { get; init; } = (25, 50);

    public (int Min, int Max) KeyPressMs { get; init; } = (50, 100);

    public (int Min, int Max) ScrollNotchMs { get; init; } = (3, 10);

    public (int Min, int Max) DragSettleMs { get; init; } = (100, 200);

    public (int Min, int Max) KeystrokeMs { get; init; } = (40, 110);

    /// <summary>Pause inserted while walking a movement path, which paces the cursor.</summary>
    public int MoveStepDelayMs { get; init; } = 1;

    /// <summary>Number of path steps emitted between pauses.</summary>
    public int MoveStepsPerDelay { get; init; } = 3;

    /// <summary>
    /// Picks a delay from an inclusive-exclusive range.
    /// </summary>
    /// <remarks>
    /// Extracted so the selection is testable on its own: a degenerate range such as
    /// (5, 5) would otherwise make <see cref="Random.Next(int, int)"/> throw, and a
    /// silently wrong bound here would quietly flatten all timing variation.
    /// </remarks>
    public static int PickDelay((int Min, int Max) range, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(range.Min, nameof(range));

        return random.Next(range.Min, Math.Max(range.Min + 1, range.Max));
    }
}
