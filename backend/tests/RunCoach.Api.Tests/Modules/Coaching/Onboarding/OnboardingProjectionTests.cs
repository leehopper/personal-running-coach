using System.Text.Json;
using FluentAssertions;
using JasperFx.Events;
using Marten;
using NSubstitute;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Entities;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Pure-function tests over the onboarding projections' Apply methods. Exercises the
/// in-memory <see cref="OnboardingProjection"/> Apply overloads and the EF
/// <see cref="UserProfileFromOnboardingProjection"/> ApplyEvent override against
/// hand-built event sequences without spinning up Marten or Postgres. The integration
/// path (Marten transaction participant + EF Core SaveChangesAsync atomicity) is
/// covered separately by the AssemblyFixture-backed integration tests landed in T01.7.
/// </summary>
public sealed class OnboardingProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void OnboardingProjection_Create_SetsInitialState()
    {
        // Arrange
        var started = new OnboardingStarted(UserId, Now);

        // Act
        var actual = OnboardingProjection.Create(started);

        // Assert
        actual.Id.Should().Be(UserId);
        actual.UserId.Should().Be(UserId);
        actual.Status.Should().Be(OnboardingStatus.InProgress);
        actual.OnboardingStartedAt.Should().Be(Now);
        actual.OutstandingClarifications.Should().BeEmpty();
        actual.Version.Should().Be(1);
    }

    [Fact]
    public void OnboardingProjection_Apply_AnswerCaptured_PrimaryGoal_SetsTypedSlot()
    {
        // Arrange
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        var expected = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.RaceTraining,
            Description = "Half marathon in October.",
        };
        var captured = new AnswerCaptured(
            OnboardingTopic.PrimaryGoal,
            JsonSerializer.SerializeToDocument(expected),
            Confidence: 0.92,
            CapturedAt: Now);

        // Act
        OnboardingProjection.Apply(captured, view);

        // Assert
        view.PrimaryGoal.Should().NotBeNull();
        view.PrimaryGoal!.Goal.Should().Be(PrimaryGoal.RaceTraining);
        view.PrimaryGoal.Description.Should().Be("Half marathon in October.");
        view.Version.Should().Be(2);
    }

    [Fact]
    public void OnboardingProjection_Apply_TopicAsked_SetsCurrentTopicAndBumpsVersion()
    {
        // Arrange — TopicAsked is the chat-surface signal for "the assistant just asked X".
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));

        // Act
        OnboardingProjection.Apply(new TopicAsked(OnboardingTopic.TargetEvent, Now.AddMinutes(1)), view);

        // Assert
        view.CurrentTopic.Should().Be(OnboardingTopic.TargetEvent);
        view.Version.Should().Be(
            2,
            because: "every Apply overload must bump Version so callers can detect drift between the view and the underlying event stream");
    }

    [Fact]
    public void OnboardingProjection_Apply_UserTurnRecorded_BumpsVersionAndPreservesSlots()
    {
        // Arrange — user turns are informational for the view; the transcript itself lives
        // on the event stream and never materializes onto OnboardingView.
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        var seededGoal = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.RaceTraining,
            Description = "Half marathon.",
        };
        OnboardingProjection.Apply(
            new AnswerCaptured(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToDocument(seededGoal),
                Confidence: 0.9,
                CapturedAt: Now),
            view);
        var versionBeforeTurn = view.Version;
        using var doc = JsonSerializer.SerializeToDocument(new { type = "text", text = "I want to PR." });

        // Act
        OnboardingProjection.Apply(new UserTurnRecorded(doc, Now.AddMinutes(2)), view);

        // Assert
        view.Version.Should().Be(versionBeforeTurn + 1);
        view.PrimaryGoal!.Goal.Should().Be(
            PrimaryGoal.RaceTraining,
            because: "informational turn events must not mutate captured-answer slots");
    }

    [Fact]
    public void OnboardingProjection_Apply_AssistantTurnRecorded_BumpsVersionAndPreservesSlots()
    {
        // Arrange — see UserTurnRecorded test; assistant turns share the same informational
        // contract on the view (transcript content is event-stream-only).
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        var versionBeforeTurn = view.Version;
        using var doc = JsonSerializer.SerializeToDocument(new { type = "text", text = "Got it." });

        // Act
        OnboardingProjection.Apply(new AssistantTurnRecorded(doc, Now.AddMinutes(3)), view);

        // Assert
        view.Version.Should().Be(versionBeforeTurn + 1);
        view.CurrentTopic.Should().BeNull(
            because: "assistant turns are informational; only TopicAsked sets the current-topic slot");
    }

    [Fact]
    public void OnboardingProjection_Apply_ClarificationRequested_AddsToList()
    {
        // Arrange
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));

        // Act
        OnboardingProjection.Apply(
            new ClarificationRequested(OnboardingTopic.TargetEvent, "Distance ambiguous", Now),
            view);

        // Assert
        view.OutstandingClarifications.Should().ContainSingle().Which.Should().Be(OnboardingTopic.TargetEvent);
    }

    [Fact]
    public void OnboardingProjection_Apply_AnswerCaptured_ClearsOutstandingClarification()
    {
        // Arrange
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        OnboardingProjection.Apply(
            new ClarificationRequested(OnboardingTopic.PrimaryGoal, "Goal ambiguous", Now),
            view);

        var resolution = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.GeneralFitness,
            Description = "Just want to feel good.",
        };

        // Act
        OnboardingProjection.Apply(
            new AnswerCaptured(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToDocument(resolution),
                Confidence: 0.88,
                CapturedAt: Now),
            view);

        // Assert
        view.OutstandingClarifications.Should().BeEmpty();
        view.PrimaryGoal!.Goal.Should().Be(PrimaryGoal.GeneralFitness);
    }

    [Fact]
    public void OnboardingProjection_Apply_PlanLinkedToUser_SetsCurrentPlanId()
    {
        // Arrange — the DEC-060 / R-069 dual-write replacement event.
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        var planId = Guid.NewGuid();

        // Act
        OnboardingProjection.Apply(new PlanLinkedToUser(UserId, planId), view);

        // Assert
        view.CurrentPlanId.Should().Be(planId);
    }

    [Fact]
    public void OnboardingProjection_Apply_OnboardingCompleted_SetsTerminalState()
    {
        // Arrange
        var view = OnboardingProjection.Create(new OnboardingStarted(UserId, Now));
        var planId = Guid.NewGuid();

        // Act
        OnboardingProjection.Apply(new OnboardingCompleted(planId, Now.AddMinutes(5)), view);

        // Assert
        view.Status.Should().Be(OnboardingStatus.Completed);
        view.OnboardingCompletedAt.Should().Be(Now.AddMinutes(5));
        view.CurrentPlanId.Should().Be(planId);
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_OnboardingStarted_CreatesNewProfile()
    {
        // Arrange
        var projection = new UserProfileFromOnboardingProjection();
        var started = new OnboardingStarted(UserId, Now);

        // Act
        var actual = projection.ApplyEvent(
            snapshot: null,
            identity: UserId,
            @event: WrapEvent(started),
            dbContext: NullDbContext(),
            session: NullSession());

        // Assert
        actual.Should().NotBeNull();
        actual!.UserId.Should().Be(UserId);
        actual.CreatedOn.Should().Be(Now);
        actual.ModifiedOn.Should().Be(Now);
        actual.OnboardingCompletedAt.Should().BeNull();
        actual.CurrentPlanId.Should().BeNull();
        actual.TenantId.Should().Be(
            UserId.ToString(),
            because: "EfCoreSingleStreamProjection requires ITenanted under TenancyStyle.Conjoined; the projection must stamp the EF row's TenantId from the event envelope so conjoined tenancy is atomic with the apply");
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_AnswerCaptured_PrimaryGoal_SetsScalarEnum()
    {
        // Arrange. The EF row stores only the `PrimaryGoal` scalar; the description
        // text lives on the Marten event stream and is not projected onto `UserProfile`.
        var projection = new UserProfileFromOnboardingProjection();
        var snapshot = new UserProfile { UserId = UserId, CreatedOn = Now, ModifiedOn = Now };
        var answer = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.RaceTraining,
            Description = "Half marathon in October.",
        };
        var captured = new AnswerCaptured(
            OnboardingTopic.PrimaryGoal,
            JsonSerializer.SerializeToDocument(answer),
            Confidence: 0.91,
            CapturedAt: Now.AddMinutes(2));

        // Act
        var actual = projection.ApplyEvent(
            snapshot,
            UserId,
            WrapEvent(captured),
            NullDbContext(),
            NullSession());

        // Assert
        actual.Should().BeSameAs(snapshot);
        actual!.PrimaryGoal.Should().Be(PrimaryGoal.RaceTraining);
        actual.ModifiedOn.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_AnswerCaptured_TargetEvent_SetsTypedRecord()
    {
        // Arrange — non-PrimaryGoal slots persist the full typed answer record.
        var projection = new UserProfileFromOnboardingProjection();
        var snapshot = new UserProfile { UserId = UserId, CreatedOn = Now, ModifiedOn = Now };
        var target = new TargetEventAnswer
        {
            EventName = "Hartford Half Marathon",
            DistanceKm = 21.0975,
            EventDateIso = "2026-10-11",
            TargetFinishTimeIso = "PT1H45M0S",
        };
        var captured = new AnswerCaptured(
            OnboardingTopic.TargetEvent,
            JsonSerializer.SerializeToDocument(target),
            Confidence: 0.95,
            CapturedAt: Now.AddMinutes(3));

        // Act
        var actual = projection.ApplyEvent(
            snapshot,
            UserId,
            WrapEvent(captured),
            NullDbContext(),
            NullSession());

        // Assert
        actual!.TargetEvent.Should().NotBeNull();
        actual.TargetEvent!.EventName.Should().Be("Hartford Half Marathon");
        actual.TargetEvent.DistanceKm.Should().Be(21.0975);
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_PrimaryGoalChangesAwayFromRaceTraining_ClearsTargetEvent()
    {
        // Arrange — runner first commits to RaceTraining + a target race, then later
        // corrects PrimaryGoal to GeneralFitness. The TargetEvent slot is only meaningful
        // under RaceTraining, so the projection must drop the stale race metadata.
        var projection = new UserProfileFromOnboardingProjection();
        var snapshot = new UserProfile
        {
            UserId = UserId,
            CreatedOn = Now,
            ModifiedOn = Now,
            PrimaryGoal = PrimaryGoal.RaceTraining,
            TargetEvent = new TargetEventAnswer
            {
                EventName = "Hartford Half Marathon",
                DistanceKm = 21.0975,
                EventDateIso = "2026-10-11",
                TargetFinishTimeIso = "PT1H45M0S",
            },
        };
        var captured = new AnswerCaptured(
            OnboardingTopic.PrimaryGoal,
            JsonSerializer.SerializeToDocument(new PrimaryGoalAnswer
            {
                Goal = PrimaryGoal.GeneralFitness,
                Description = "Just want to stay healthy.",
            }),
            Confidence: 0.92,
            CapturedAt: Now.AddMinutes(5));

        // Act
        var actual = projection.ApplyEvent(
            snapshot,
            UserId,
            WrapEvent(captured),
            NullDbContext(),
            NullSession());

        // Assert
        actual!.PrimaryGoal.Should().Be(PrimaryGoal.GeneralFitness);
        actual.TargetEvent.Should().BeNull(
            because: "TargetEvent is only meaningful under RaceTraining; switching to a non-race goal must drop the stale race metadata");
    }

    [Fact]
    public void OnboardingProjection_PrimaryGoalChangesAwayFromRaceTraining_ClearsTargetEventOnView()
    {
        // Arrange — same invariant on the in-memory `OnboardingView`. Replays
        // RaceTraining → TargetEvent → GeneralFitness and asserts the view drops
        // TargetEvent so the chat surface and EF row stay in sync.
        var view = new OnboardingView
        {
            UserId = UserId,
            PrimaryGoal = new PrimaryGoalAnswer
            {
                Goal = PrimaryGoal.RaceTraining,
                Description = "Half marathon.",
            },
            TargetEvent = new TargetEventAnswer
            {
                EventName = "Hartford Half Marathon",
                DistanceKm = 21.0975,
                EventDateIso = "2026-10-11",
                TargetFinishTimeIso = "PT1H45M0S",
            },
        };
        var captured = new AnswerCaptured(
            OnboardingTopic.PrimaryGoal,
            JsonSerializer.SerializeToDocument(new PrimaryGoalAnswer
            {
                Goal = PrimaryGoal.GeneralFitness,
                Description = "Just want to stay healthy.",
            }),
            Confidence: 0.92,
            CapturedAt: Now.AddMinutes(5));

        // Act
        OnboardingProjection.Apply(captured, view);

        // Assert
        view.PrimaryGoal!.Goal.Should().Be(PrimaryGoal.GeneralFitness);
        view.TargetEvent.Should().BeNull(
            because: "OnboardingView mirrors the EF projection invariant: TargetEvent only persists under RaceTraining");
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_PlanLinkedToUser_SetsCurrentPlanId()
    {
        // Arrange — the DEC-060 / R-069 atomic dual-write replacement: this single event
        // is what drives the EF `UserProfile.CurrentPlanId` write inside Marten's transaction.
        var projection = new UserProfileFromOnboardingProjection();
        var snapshot = new UserProfile { UserId = UserId, CreatedOn = Now, ModifiedOn = Now };
        var planId = Guid.NewGuid();
        var linked = new PlanLinkedToUser(UserId, planId);
        var envelope = WrapEvent(linked, timestamp: Now.AddMinutes(10));

        // Act
        var actual = projection.ApplyEvent(snapshot, UserId, envelope, NullDbContext(), NullSession());

        // Assert
        actual!.CurrentPlanId.Should().Be(planId);
        actual.ModifiedOn.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_OnboardingCompleted_SetsCompletionTimestamp()
    {
        // Arrange
        var projection = new UserProfileFromOnboardingProjection();
        var planId = Guid.NewGuid();
        var snapshot = new UserProfile
        {
            UserId = UserId,
            CreatedOn = Now,
            ModifiedOn = Now,
            CurrentPlanId = planId,
        };
        var completed = new OnboardingCompleted(planId, Now.AddMinutes(15));

        // Act
        var actual = projection.ApplyEvent(snapshot, UserId, WrapEvent(completed), NullDbContext(), NullSession());

        // Assert
        actual!.OnboardingCompletedAt.Should().Be(Now.AddMinutes(15));
        actual.CurrentPlanId.Should().Be(planId);
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_TurnEvents_NoStateChange()
    {
        // Arrange — chat-transcript events live on the Marten stream only; they must not
        // dirty the EF change tracker.
        var projection = new UserProfileFromOnboardingProjection();
        var snapshot = new UserProfile { UserId = UserId, CreatedOn = Now, ModifiedOn = Now };
        using var doc = JsonSerializer.SerializeToDocument(new { type = "text", text = "hi" });
        var userTurn = new UserTurnRecorded(doc, Now.AddMinutes(1));

        // Act
        var actual = projection.ApplyEvent(snapshot, UserId, WrapEvent(userTurn), NullDbContext(), NullSession());

        // Assert
        actual.Should().BeSameAs(snapshot);
        actual!.ModifiedOn.Should().Be(Now);
    }

    [Fact]
    public void UserProfileFromOnboardingProjection_ApplyEvent_FullSequence_LandsAllSlotsAndPlanId()
    {
        // Arrange — assemble the canonical eight-event onboarding sequence and replay through
        // the projection as the production Marten codegen would. Asserts the terminal EF row
        // matches what an integration test would observe after `SaveChangesAsync`.
        var projection = new UserProfileFromOnboardingProjection();
        var planId = Guid.NewGuid();
        UserProfile? snapshot = null;

        var events = BuildFullSequence(planId);

        // Act
        foreach (var (data, ts) in events)
        {
            snapshot = projection.ApplyEvent(snapshot, UserId, WrapEvent(data, ts), NullDbContext(), NullSession());
        }

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.PrimaryGoal.Should().Be(PrimaryGoal.RaceTraining);
        snapshot.TargetEvent.Should().NotBeNull();
        snapshot.CurrentFitness.Should().NotBeNull();
        snapshot.WeeklySchedule.Should().NotBeNull();
        snapshot.InjuryHistory.Should().NotBeNull();
        snapshot.Preferences.Should().NotBeNull();
        snapshot.CurrentPlanId.Should().Be(planId);
        snapshot.OnboardingCompletedAt.Should().NotBeNull();
    }

    private static List<(object Data, DateTimeOffset Timestamp)> BuildFullSequence(Guid planId)
    {
        var t = Now;
        var events = new List<(object Data, DateTimeOffset Timestamp)>
        {
            (new OnboardingStarted(UserId, t), t),
            (new TopicAsked(OnboardingTopic.PrimaryGoal, t.AddSeconds(1)), t.AddSeconds(1)),
            (Capture(
                OnboardingTopic.PrimaryGoal,
                new PrimaryGoalAnswer
                {
                    Goal = PrimaryGoal.RaceTraining,
                    Description = "Half marathon",
                },
                t.AddSeconds(5)), t.AddSeconds(5)),
            (Capture(
                OnboardingTopic.TargetEvent,
                new TargetEventAnswer
                {
                    EventName = "Hartford",
                    DistanceKm = 21.0975,
                    EventDateIso = "2026-10-11",
                    TargetFinishTimeIso = "PT1H45M0S",
                },
                t.AddSeconds(10)), t.AddSeconds(10)),
            (Capture(
                OnboardingTopic.CurrentFitness,
                new CurrentFitnessAnswer
                {
                    TypicalWeeklyKm = 30,
                    LongestRecentRunKm = 12,
                    RecentRaceDistanceKm = null,
                    RecentRaceTimeIso = null,
                    Description = "Comfortable at easy pace, no recent races.",
                },
                t.AddSeconds(15)), t.AddSeconds(15)),
            (Capture(
                OnboardingTopic.WeeklySchedule,
                new WeeklyScheduleAnswer
                {
                    MaxRunDaysPerWeek = 4,
                    TypicalSessionMinutes = 60,
                    Monday = true,
                    Tuesday = false,
                    Wednesday = true,
                    Thursday = false,
                    Friday = true,
                    Saturday = false,
                    Sunday = true,
                    Description = string.Empty,
                },
                t.AddSeconds(20)), t.AddSeconds(20)),
            (Capture(
                OnboardingTopic.InjuryHistory,
                new InjuryHistoryAnswer
                {
                    HasActiveInjury = false,
                    ActiveInjuryDescription = string.Empty,
                    PastInjurySummary = string.Empty,
                },
                t.AddSeconds(25)), t.AddSeconds(25)),
            (Capture(
                OnboardingTopic.Preferences,
                new PreferencesAnswer
                {
                    PreferredUnits = PreferredUnits.Kilometers,
                    PreferTrail = false,
                    ComfortableWithIntensity = true,
                    Description = string.Empty,
                },
                t.AddSeconds(30)), t.AddSeconds(30)),
            (new PlanLinkedToUser(UserId, planId), t.AddSeconds(60)),
            (new OnboardingCompleted(planId, t.AddSeconds(61)), t.AddSeconds(61)),
        };
        return events;
    }

    private static AnswerCaptured Capture<T>(OnboardingTopic topic, T answer, DateTimeOffset at)
    {
        return new AnswerCaptured(topic, JsonSerializer.SerializeToDocument(answer), Confidence: 0.9, CapturedAt: at);
    }

    private static IEvent WrapEvent(object data, DateTimeOffset? timestamp = null, string? tenantId = null)
    {
        var stub = Substitute.For<IEvent>();
        stub.Data.Returns(data);
        stub.Timestamp.Returns(timestamp ?? Now);
        stub.StreamId.Returns(UserId);

        // DEC-047 conjoined-tenancy id: `tenantId == streamId.ToString()`. Tests default
        // to that shape so the EF projection's `snapshot.TenantId = @event.TenantId`
        // stamping has a deterministic value to assert against.
        stub.TenantId.Returns(tenantId ?? UserId.ToString());
        return stub;
    }

    // The projection's apply logic does not invoke any DbContext / session methods on the
    // happy path; a default-options DbContext + substituted session satisfy the override
    // signature without requiring a real Npgsql connection. We never call SaveChangesAsync
    // here because the apply methods only mutate the in-memory snapshot.
    private static RunCoachDbContext NullDbContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptions<RunCoachDbContext>();
        return new RunCoachDbContext(options);
    }

    private static IQuerySession NullSession() => Substitute.For<IQuerySession>();
}
