namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Where a logged workout's derived pace falls relative to its prescribed
/// Fast/Slow band (Slice 3 PR2 / Unit 1). Pace deviation is reported as band
/// <em>membership</em> rather than a scalar diff: a run inside the band is on
/// target regardless of the exact second-per-km. Members are explicitly numbered
/// so reordering never shifts a serialized encoding.
/// </summary>
public enum PaceBandMembership
{
    /// <summary>The derived pace sits within the prescribed Fast/Slow band — on target.</summary>
    InsideBand = 0,

    /// <summary>The derived pace is faster than the band's Fast bound (lower sec/km).</summary>
    FasterThanFast = 1,

    /// <summary>The derived pace is slower than the band's Slow bound (higher sec/km).</summary>
    SlowerThanSlow = 2,

    /// <summary>
    /// No pace could be derived (a skipped run, or a zero/near-zero distance or
    /// duration) — there is no pace signal, and no spurious pace deviation is produced.
    /// </summary>
    Unknown = 3,
}
