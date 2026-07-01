using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// The labelled ground-truth set for the conversation intent-classifier accuracy
/// eval (Slice 4B Unit 7). Covers the four canonical question shapes
/// (status / injury / schedule / intensity) plus workout-log reports and
/// deliberately ambiguous messages, with at least six per class so the 3x3
/// confusion matrix is statistically meaningful (mirrors the adaptation suite's
/// per-category minimum). Labels follow the <c>conversation-classifier.v1</c>
/// prompt rules: a message is <see cref="MessageIntent.WorkoutLog"/> only when its
/// date, distance, and duration are all cleanly resolvable; anything missing one of
/// those, or that could be a question or a log, is <see cref="MessageIntent.Ambiguous"/>
/// (bias-to-ask, DEC-085).
/// </summary>
internal static class IntentScenarioLibrary
{
    /// <summary>The minimum number of labelled scenarios required per intent class.</summary>
    internal const int MinimumPerClass = 6;

    /// <summary>Gets the labelled scenarios (the committed zero-regression baseline).</summary>
    internal static IReadOnlyList<IntentScenario> Scenarios { get; } =
    [
        new("question.status.block-progress", "How's my training going so far this block?", MessageIntent.Question),
        new("question.status.on-track", "Am I on track for my goal race?", MessageIntent.Question),

        // ── Question: schedule (what's next / what's the week) ────────────────
        new("question.schedule.tomorrow", "What workout do I have tomorrow?", MessageIntent.Question),
        new("question.schedule.week-shape", "What does my week look like?", MessageIntent.Question),

        // ── Question: intensity (how hard / what pace) ────────────────────────
        new("question.intensity.tempo-pace", "What pace should I target for my tempo this week?", MessageIntent.Question),
        new("question.intensity.easy-or-hard", "Should tomorrow's run be easy or should I push it?", MessageIntent.Question),

        // ── Question: injury (asking, not reporting a run) ────────────────────
        new("question.injury.calf-downhills", "My calf has been tight on downhills lately. Should I worry about sticking to the plan?", MessageIntent.Question),
        new("question.injury.achilles-niggle", "Is it safe to keep training with a niggle in my Achilles, or should I back off?", MessageIntent.Question),

        // ── WorkoutLog: date + distance + duration all clear ──────────────────
        new("log.10k-this-morning", "Ran 10k this morning in 52:30, felt good.", MessageIntent.WorkoutLog),
        new("log.8mi-long-yesterday", "Did my 8 mile long run yesterday in 1 hour 6 minutes.", MessageIntent.WorkoutLog),
        new("log.5k-today", "Knocked out 5k today in 24:10.", MessageIntent.WorkoutLog),
        new("log.12km-heavy-legs", "This morning I ran 12 km in 58 minutes, legs were heavy.", MessageIntent.WorkoutLog),
        new("log.6mi-tempo-yesterday", "Completed my 6 mile tempo yesterday, 42:30 total.", MessageIntent.WorkoutLog),
        new("log.4mi-easy-today", "Went for an easy 4 miler today, took me 36 minutes.", MessageIntent.WorkoutLog),

        // ── Ambiguous: missing a required field or could be either ────────────
        new("ambiguous.rough-out-there", "Today was rough out there.", MessageIntent.Ambiguous),
        new("ambiguous.got-run-in", "Got my run in this morning.", MessageIntent.Ambiguous),
        new("ambiguous.felt-strong", "Felt strong on the roads today.", MessageIntent.Ambiguous),
        new("ambiguous.about-5k-no-time", "I ran about 5k earlier.", MessageIntent.Ambiguous),
        new("ambiguous.40-min-no-distance", "Did 40 minutes today.", MessageIntent.Ambiguous),
        new("ambiguous.couldnt-finish", "Couldn't finish my session.", MessageIntent.Ambiguous),
    ];
}
