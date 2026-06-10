using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// One logged workout in an escalation scenario: the prescribed
/// <see cref="WorkoutType"/> (which also decides the key-workout flag and the
/// pace zone the actuals are measured against), the <see cref="DeviationIntent"/>
/// the runner realizes against that band, and the day offset from the scenario's
/// base date (so multi-step scenarios advance the consecutive-miss and cooldown
/// clocks the hysteresis depends on).
/// </summary>
/// <param name="WorkoutType">The prescribed workout type.</param>
/// <param name="Intent">The deviation the step should produce.</param>
/// <param name="DayOffset">Days after the scenario base date this workout occurred.</param>
internal sealed record EscalationScenarioStep(
    WorkoutType WorkoutType,
    DeviationIntent Intent,
    int DayOffset);
