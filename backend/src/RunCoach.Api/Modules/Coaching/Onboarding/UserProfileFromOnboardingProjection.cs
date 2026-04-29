using System.Text.Json;
using JasperFx.Events;
using Marten;
using Marten.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// EF Core single-stream projection that materializes the runner's onboarding events into
/// the relational <see cref="RunnerOnboardingProfile"/> row (spec 13 § Unit 1, R01.5; DEC-060 / R-069).
/// Marten.EntityFrameworkCore 8.32 wires this projection as a transaction participant on
/// the same <c>NpgsqlConnection</c> as the Marten event append, so the EF write commits
/// inside Marten's transaction — atomic by construction, no dual-write across stores.
/// </summary>
/// <remarks>
/// <para>
/// Per the package documentation: the projection "creates a per-slice DbContext using the
/// same PostgreSQL connection as the Marten session" and "registers a transaction
/// participant so the DbContext's SaveChangesAsync is called within Marten's transaction,
/// ensuring atomicity." The DEC-060 dual-write replacement turns the prior direct EF
/// mutation inside the Wolverine handler into a <see cref="PlanLinkedToUser"/> event
/// applied here.
/// </para>
/// <para>
/// The projection is registered with <c>ProjectionLifecycle.Inline</c> in
/// <see cref="MartenConfiguration"/> via <c>opts.Projections.Add(new
/// UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline)</c>, which the
/// Marten.EntityFrameworkCore extension method handles transparently.
/// </para>
/// </remarks>
public sealed class UserProfileFromOnboardingProjection
    : EfCoreSingleStreamProjection<RunnerOnboardingProfile, Guid, RunCoachDbContext>
{
    /// <summary>
    /// Pattern-matches every onboarding event type and mutates the <paramref name="snapshot"/>
    /// row in place (or constructs it on the very first event). Returns the mutated snapshot
    /// so Marten upserts it through the EF DbContext as part of the transaction.
    /// </summary>
    /// <param name="snapshot">
    /// The current EF row (or <see langword="null"/> if no row exists yet — the first
    /// <see cref="OnboardingStarted"/> seeds it). Type is <see cref="RunnerOnboardingProfile"/>.
    /// </param>
    /// <param name="identity">The stream id (the runner's user id).</param>
    /// <param name="event">The Marten event envelope.</param>
    /// <param name="dbContext">The per-slice EF DbContext bound to the Marten session connection.</param>
    /// <param name="session">The Marten query session for cross-aggregate lookups (unused here).</param>
    /// <returns>The mutated snapshot.</returns>
    public override RunnerOnboardingProfile? ApplyEvent(
        RunnerOnboardingProfile? snapshot,
        Guid identity,
        IEvent @event,
        RunCoachDbContext dbContext,
        IQuerySession session)
    {
        _ = dbContext;
        _ = session;

        switch (@event.Data)
        {
            case OnboardingStarted started:
                snapshot = EnsureProfile(snapshot, identity, started.StartedAt, @event.TenantId);
                return snapshot;

            case AnswerCaptured captured:
                snapshot = EnsureProfile(snapshot, identity, captured.CapturedAt, @event.TenantId);
                ApplyAnswerCaptured(snapshot, captured);
                return snapshot;

            case PlanLinkedToUser linked:
                snapshot = EnsureProfile(snapshot, identity, @event.Timestamp, @event.TenantId);
                snapshot.CurrentPlanId = linked.PlanId;
                return snapshot;

            case OnboardingCompleted completed:
                snapshot = EnsureProfile(snapshot, identity, completed.CompletedAt, @event.TenantId);
                snapshot.OnboardingCompletedAt = completed.CompletedAt;
                snapshot.CurrentPlanId ??= completed.PlanId;
                return snapshot;

            // Conversational turns and clarification requests do not change EF state -
            // the chat transcript lives on the Marten event stream only. Returning the
            // snapshot unchanged keeps the row out of the EF change tracker.
            case TopicAsked:
            case UserTurnRecorded:
            case AssistantTurnRecorded:
            case ClarificationRequested:
                return snapshot;

            default:
                return snapshot;
        }
    }

    private static RunnerOnboardingProfile EnsureProfile(RunnerOnboardingProfile? snapshot, Guid identity, DateTimeOffset timestamp, string tenantId)
    {
        snapshot ??= NewProfile(identity, timestamp, tenantId);
        snapshot.TenantId = tenantId;
        snapshot.ModifiedOn = timestamp;
        return snapshot;
    }

    private static RunnerOnboardingProfile NewProfile(Guid userId, DateTimeOffset createdOn, string tenantId)
    {
        return new RunnerOnboardingProfile
        {
            UserId = userId,
            TenantId = tenantId,
            CreatedOn = createdOn,
            ModifiedOn = createdOn,
        };
    }

    private static void ApplyAnswerCaptured(RunnerOnboardingProfile snapshot, AnswerCaptured captured)
    {
        switch (captured.Topic)
        {
            case OnboardingTopic.PrimaryGoal:
                {
                    var typed = DeserializeRequiredPayload<PrimaryGoalAnswer>(captured.NormalizedPayload, captured.Topic);
                    snapshot.PrimaryGoal = typed.Goal;
                    if (typed.Goal != Models.PrimaryGoal.RaceTraining)
                    {
                        snapshot.TargetEvent = null;
                    }

                    break;
                }

            case OnboardingTopic.TargetEvent:
                snapshot.TargetEvent = DeserializeRequiredPayload<TargetEventAnswer>(captured.NormalizedPayload, captured.Topic);
                break;

            case OnboardingTopic.CurrentFitness:
                snapshot.CurrentFitness = DeserializeRequiredPayload<CurrentFitnessAnswer>(captured.NormalizedPayload, captured.Topic);
                break;

            case OnboardingTopic.WeeklySchedule:
                snapshot.WeeklySchedule = DeserializeRequiredPayload<WeeklyScheduleAnswer>(captured.NormalizedPayload, captured.Topic);
                break;

            case OnboardingTopic.InjuryHistory:
                snapshot.InjuryHistory = DeserializeRequiredPayload<InjuryHistoryAnswer>(captured.NormalizedPayload, captured.Topic);
                break;

            case OnboardingTopic.Preferences:
                snapshot.Preferences = DeserializeRequiredPayload<PreferencesAnswer>(captured.NormalizedPayload, captured.Topic);
                break;

            default:
                throw new InvalidOperationException($"Unknown OnboardingTopic value '{captured.Topic}' encountered in EF projection");
        }
    }

    private static T DeserializeRequiredPayload<T>(JsonDocument payload, OnboardingTopic topic)
        where T : class
    {
        var result = payload.Deserialize<T>();
        if (result is null)
        {
            throw new InvalidOperationException(
                $"AnswerCaptured event for topic '{topic}' has a null or unparseable payload of type {typeof(T).Name}; refusing to apply to avoid silent state corruption.");
        }

        return result;
    }
}
