namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The deviation a scenario step intends to produce. The runner translates each
/// intent into concrete actuals relative to the step's profile-resolved pace band
/// (so the same intent is realized correctly whatever the runner's Daniels-Gilbert
/// zones are), then feeds them through the real <c>DeviationEngine</c>. This keeps
/// scenarios band-agnostic while still exercising the production deviation math.
/// </summary>
internal enum DeviationIntent
{
    /// <summary>Completed on distance and inside the pace band — no deviation.</summary>
    OnTarget = 0,

    /// <summary>Completed with a sub-tolerance drift (~2%) — still absorbed as on-target.</summary>
    WithinTolerance = 1,

    /// <summary>Completed but slower than the band's slow bound — a single minor under-performance.</summary>
    MinorSlow = 2,

    /// <summary>Completed but well short of the prescribed distance (beyond tolerance) — under-performs.</summary>
    ShortDistance = 3,

    /// <summary>Cut short (partial completion) — under-performs, but never counts as a missed day.</summary>
    Partial = 4,

    /// <summary>Completed faster than the band's fast bound — over-performance (never upgrades).</summary>
    OverPerform = 5,

    /// <summary>Skipped entirely — under-performs and counts toward the consecutive-missed streak.</summary>
    Missed = 6,
}
