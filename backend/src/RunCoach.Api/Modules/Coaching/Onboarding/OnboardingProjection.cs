using System.Text.Json;
using Marten.Events.Aggregation;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Inline single-stream projection that materializes the runner's in-flight onboarding state
/// into an <see cref="OnboardingView"/> Marten document (spec 13 § Unit 1, R01.4). Marten's
/// codegen wires each <c>Apply</c> overload below to the corresponding event type via
/// pattern-matching at startup; the document is upserted on the same <c>IDocumentSession</c>
/// as the event append, preserving atomicity for the per-turn handler.
/// </summary>
/// <remarks>
/// <para>
/// The projection is registered with <c>ProjectionLifecycle.Inline</c> in
/// <see cref="Infrastructure.MartenConfiguration"/>. The deterministic next-topic selector
/// and the completion gate read this view rather than replaying the stream on every turn.
/// </para>
/// <para>
/// The <see cref="PlanLinkedToUser"/> apply branch mirrors the <see cref="UserProfileFromOnboardingProjection"/>
/// branch so the in-memory view stays in sync with the EF projection (DEC-060 / R-069).
/// </para>
/// </remarks>
public sealed class OnboardingProjection : SingleStreamProjection<OnboardingView, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingProjection"/> class. Marten's
    /// inline registration constructs the projection at startup; no DI wiring is needed
    /// because pure event-application logic carries no dependencies.
    /// </summary>
    public OnboardingProjection()
    {
        // Marten infers the projection name from the type, but pinning it here makes the
        // schema artifact name stable across rename refactors that might otherwise break
        // production projection-state rows on a redeploy.
        Name = "OnboardingProjection";
    }

    /// <summary>Initializes the view with the stream-creation timestamp and user id.</summary>
    /// <param name="event">The stream-creation event.</param>
    /// <returns>The initial <see cref="OnboardingView"/> document for the stream.</returns>
    public static OnboardingView Create(OnboardingStarted @event)
    {
        return new OnboardingView
        {
            Id = @event.UserId,
            UserId = @event.UserId,
            Status = OnboardingStatus.InProgress,
            OnboardingStartedAt = @event.StartedAt,
            OutstandingClarifications = Array.Empty<OnboardingTopic>(),
            Version = 1,
        };
    }

    /// <summary>Records the most recently asked topic so the chat surface can highlight it.</summary>
    public static void Apply(TopicAsked @event, OnboardingView view)
    {
        view.CurrentTopic = @event.Topic;
        view.Version++;
    }

    /// <summary>
    /// User-turn append is informational for the view — the turn content lives on the
    /// event stream itself. The version bump records that something changed so callers
    /// can detect drift.
    /// </summary>
    public static void Apply(UserTurnRecorded @event, OnboardingView view)
    {
        _ = @event;
        view.Version++;
    }

    /// <summary>Assistant-turn append is informational for the view; see <see cref="Apply(UserTurnRecorded, OnboardingView)"/>.</summary>
    public static void Apply(AssistantTurnRecorded @event, OnboardingView view)
    {
        _ = @event;
        view.Version++;
    }

    /// <summary>
    /// Captures a normalized topic answer onto the view's typed slot and clears any
    /// outstanding clarification for that topic.
    /// </summary>
    public static void Apply(AnswerCaptured @event, OnboardingView view)
    {
        switch (@event.Topic)
        {
            case OnboardingTopic.PrimaryGoal:
                {
                    var typed = DeserializePayload<PrimaryGoalAnswer>(@event.NormalizedPayload);
                    view.PrimaryGoal = typed;

                    // TargetEvent is only meaningful when PrimaryGoal == RaceTraining. If the
                    // runner switches off race training (e.g. RaceTraining -> GeneralFitness),
                    // a previously-captured TargetEvent must be cleared to avoid stale race
                    // metadata on the view. A null payload (malformed event) must not silently
                    // nuke TargetEvent — only clear when deserialization succeeded.
                    if (typed is not null && typed.Goal != Models.PrimaryGoal.RaceTraining)
                    {
                        view.TargetEvent = null;
                    }

                    break;
                }

            case OnboardingTopic.TargetEvent:
                view.TargetEvent = DeserializePayload<TargetEventAnswer>(@event.NormalizedPayload);
                break;

            case OnboardingTopic.CurrentFitness:
                view.CurrentFitness = DeserializePayload<CurrentFitnessAnswer>(@event.NormalizedPayload);
                break;

            case OnboardingTopic.WeeklySchedule:
                view.WeeklySchedule = DeserializePayload<WeeklyScheduleAnswer>(@event.NormalizedPayload);
                break;

            case OnboardingTopic.InjuryHistory:
                view.InjuryHistory = DeserializePayload<InjuryHistoryAnswer>(@event.NormalizedPayload);
                break;

            case OnboardingTopic.Preferences:
                view.Preferences = DeserializePayload<PreferencesAnswer>(@event.NormalizedPayload);
                break;

            default:
                throw new InvalidOperationException($"Unknown OnboardingTopic value '{{@event.Topic}}' encountered in onboarding projection");
        }

        if (view.OutstandingClarifications.Contains(@event.Topic))
        {
            view.OutstandingClarifications = view.OutstandingClarifications
                .Where(t => t != @event.Topic)
                .ToArray();
        }

        view.Version++;
    }

    /// <summary>
    /// Records a pending clarification on the view; the deterministic completion gate
    /// fails while this list is non-empty.
    /// </summary>
    public static void Apply(ClarificationRequested @event, OnboardingView view)
    {
        if (!view.OutstandingClarifications.Contains(@event.Topic))
        {
            view.OutstandingClarifications = view.OutstandingClarifications
                .Append(@event.Topic)
                .ToArray();
        }

        view.Version++;
    }

    /// <summary>
    /// Mirrors the <see cref="UserProfileFromOnboardingProjection"/> branch so the in-memory
    /// view exposes the active plan id without a cross-store read (DEC-060 / R-069).
    /// </summary>
    public static void Apply(PlanLinkedToUser @event, OnboardingView view)
    {
        view.CurrentPlanId = @event.PlanId;
        view.Version++;
    }

    /// <summary>Closes the stream — sets terminal status and records the completion timestamp.</summary>
    public static void Apply(OnboardingCompleted @event, OnboardingView view)
    {
        view.Status = OnboardingStatus.Completed;
        view.OnboardingCompletedAt = @event.CompletedAt;
        view.CurrentPlanId ??= @event.PlanId;
        view.Version++;
    }

    private static T? DeserializePayload<T>(JsonDocument payload)
        where T : class
    {
        return payload.Deserialize<T>();
    }
}
