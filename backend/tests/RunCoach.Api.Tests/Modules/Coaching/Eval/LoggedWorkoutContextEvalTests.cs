using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Constants;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Slice-2b Unit 5 eval proof artifact: verifies that a recent logged workout's
/// freeform note and metric values flow into the assembled coaching prompt
/// (DEC-076 / brainstorm D). The assertion is on the deterministic assembled
/// prompt text, so it runs in Replay-mode CI with zero API calls.
/// </summary>
[Trait("Category", "Eval")]
public sealed class LoggedWorkoutContextEvalTests : EvalTestBase
{
    [Fact]
    public async Task AssembledPrompt_WithRecentLoggedWorkout_ContainsNoteAndMetricValues()
    {
        // Arrange — an intermediate runner with one recent logged workout
        // carrying a freeform note plus heart-rate and RPE.
        var profile = LoadProfile("lee");
        const string note = "tempo clicked in the back half, strong finish";
        var loggedWorkouts = ImmutableArray.Create(new LoggedWorkoutDetail(
            new DateOnly(2026, 6, 1),
            "Tempo",
            Distance.FromKilometers(8),
            Duration.FromMinutes(40),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkoutMetricKeys.HrAvg] = "151",
                [WorkoutMetricKeys.Rpe] = "7",
            },
            note));

        // Act — build the coaching prompt (no LLM call; Replay-safe).
        var assembled = await AssembleContextWithLoggedWorkoutsAsync(
            profile,
            loggedWorkouts,
            ct: TestContext.Current.CancellationToken);
        var promptText = BuildUserMessageFromSections(assembled);

        // Assert — the logged workout's note and metric values reach the prompt
        // the coaching LLM receives.
        promptText.Should().Contain(note);
        promptText.Should().Contain("HR 151");
        promptText.Should().Contain("RPE 7");
    }
}
