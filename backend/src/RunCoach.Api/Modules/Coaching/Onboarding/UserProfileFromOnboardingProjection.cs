using System.Text.Json;
using JasperFx.Events;
using Marten;
using Marten.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Entities;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// EF Core single-stream projection that materializes the runner's onboarding events into
/// the relational <see cref="UserProfile"/> row (spec 13 § Unit 1, R01.5; DEC-060 / R-069).
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
    : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>
{
    /// <summary>
    /// Pattern-matches every onboarding event type and mutates the <paramref name="snapshot"/>
    /// row in place (or constructs it on the very first event). Returns the mutated snapshot
    /// so Marten upserts it through the EF DbContext as part of the transaction.
    /// </summary>
    /// <param name="snapshot">
    /// The current EF row (or <see langword="null"/> if no row exists yet — the first
    /// <see cref="OnboardingStarted"/> seeds it).
    /// </param>
    /// <param name="identity">The stream id (the runner's user id).</param>
    /// <param name="event">The Marten event envelope.</param>
    /// <param name="dbContext">The per-slice EF DbContext bound to the Marten session connection.</param>
    /// <param name="session">The Marten query session for cross-aggregate lookups (unused here).</param>
    /// <returns>The mutated snapshot.</returns>
    public override UserProfile? ApplyEvent(
        UserProfile? snapshot,
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
                snapshot ??= new UserProfile
                {
                    UserId = identity,
                    CreatedOn = started.StartedAt,
                };
                snapshot.TenantId = @event.TenantId;
                snapshot.ModifiedOn = started.StartedAt;
                return snapshot;

            case AnswerCaptured captured:
                snapshot ??= NewProfile(identity, captured.CapturedAt, @event.TenantId);
                snapshot.TenantId = @event.TenantId;
                ApplyAnswerCaptured(snapshot, captured);
                snapshot.ModifiedOn = captured.CapturedAt;
                return snapshot;

            case PlanLinkedToUser linked:
                snapshot ??= NewProfile(identity, @event.Timestamp, @event.TenantId);
                snapshot.TenantId = @event.TenantId;
                snapshot.CurrentPlanId = linked.PlanId;
                snapshot.ModifiedOn = @event.Timestamp;
                return snapshot;

            case OnboardingCompleted completed:
                snapshot ??= NewProfile(identity, completed.CompletedAt, @event.TenantId);
                snapshot.TenantId = @event.TenantId;
                snapshot.OnboardingCompletedAt = completed.CompletedAt;
                snapshot.CurrentPlanId ??= completed.PlanId;
                snapshot.ModifiedOn = completed.CompletedAt;
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

    private static UserProfile NewProfile(Guid userId, DateTimeOffset createdOn, string tenantId)
    {
        return new UserProfile
        {
            UserId = userId,
            TenantId = tenantId,
            CreatedOn = createdOn,
            ModifiedOn = createdOn,
        };
    }

    private static void ApplyAnswerCaptured(UserProfile snapshot, AnswerCaptured captured)
    {
        switch (captured.Topic)
        {
            case OnboardingTopic.PrimaryGoal:
                {
                    var typed = captured.NormalizedPayload.Deserialize<PrimaryGoalAnswer>();
                    snapshot.PrimaryGoal = typed?.Goal;
                    break;
                }

            case OnboardingTopic.TargetEvent:
                {
                    var typed = captured.NormalizedPayload.Deserialize<TargetEventAnswer>();
                    if (typed is not null)
                    {
                        snapshot.TargetEvent = typed;
                    }

                    break;
                }

            case OnboardingTopic.CurrentFitness:
                {
                    var typed = captured.NormalizedPayload.Deserialize<CurrentFitnessAnswer>();
                    if (typed is not null)
                    {
                        typed.EnsureValid();
                        snapshot.CurrentFitness = typed;
                    }

                    break;
                }

            case OnboardingTopic.WeeklySchedule:
                {
                    var typed = captured.NormalizedPayload.Deserialize<WeeklyScheduleAnswer>();
                    if (typed is not null)
                    {
                        snapshot.WeeklySchedule = typed;
                    }

                    break;
                }

            case OnboardingTopic.InjuryHistory:
                {
                    var typed = captured.NormalizedPayload.Deserialize<InjuryHistoryAnswer>();
                    if (typed is not null)
                    {
                        snapshot.InjuryHistory = typed;
                    }

                    break;
                }

            case OnboardingTopic.Preferences:
                {
                    var typed = captured.NormalizedPayload.Deserialize<PreferencesAnswer>();
                    if (typed is not null)
                    {
                        snapshot.Preferences = typed;
                    }

                    break;
                }
        }
    }
}
