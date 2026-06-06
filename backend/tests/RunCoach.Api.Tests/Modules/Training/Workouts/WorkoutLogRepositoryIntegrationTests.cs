using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Integration tests for the <see cref="WorkoutLog"/> persistence foundation
/// (slice-2b Unit 2 / PR2). Each test seeds a real Identity user, then round-trips
/// a log through <see cref="IWorkoutLogRepository"/> against the Testcontainers
/// Postgres with the <c>AddWorkoutLog</c> migration applied on fixture boot.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class WorkoutLogRepositoryIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateAsync_MinimumPayload_RoundTripsAndLeavesOptionalsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var log = NewLog(userId, new DateOnly(2026, 6, 1));

        // Act
        await CreateAsync(log, ct);
        var actual = await GetByIdAsync(userId, log.WorkoutLogId, ct);

        // Assert
        actual.Should().NotBeNull();
        actual!.Distance.Meters.Should().Be(5000.0);
        actual.Duration.TotalMinutes.Should().Be(25.0);
        actual.CompletionStatus.Should().Be(CompletionStatus.Complete);
        actual.Metrics.Should().BeNull();
        actual.Splits.Should().BeNull();
        actual.Notes.Should().BeNull();
        actual.Prescription.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_RichPayload_PersistsMetricsBagAndTypedSplits()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var log = NewLog(userId, new DateOnly(2026, 6, 2));
        log.Metrics = """{"hrAvg":142,"rpe":7,"cadence":178}""";
        log.Splits =
        [
            new WorkoutSplit(1, 1000.0, 300.0, 300.0, 138),
            new WorkoutSplit(2, 1000.0, 295.0, 295.0, 145),
        ];

        // Act
        await CreateAsync(log, ct);
        var actual = await GetByIdAsync(userId, log.WorkoutLogId, ct);

        // Assert — metrics jsonb round-trips by value (key order is not significant).
        actual.Should().NotBeNull();
        using var metrics = JsonDocument.Parse(actual!.Metrics!);
        metrics.RootElement.GetProperty("hrAvg").GetInt32().Should().Be(142);
        metrics.RootElement.GetProperty("rpe").GetInt32().Should().Be(7);
        metrics.RootElement.GetProperty("cadence").GetInt32().Should().Be(178);

        // Typed splits round-trip with their index/distance/duration/pace.
        actual.Splits.Should().NotBeNull();
        actual.Splits!.Should().HaveCount(2);
        actual.Splits![0].Should().Be(new WorkoutSplit(1, 1000.0, 300.0, 300.0, 138));
        actual.Splits![1].Should().Be(new WorkoutSplit(2, 1000.0, 295.0, 295.0, 145));
    }

    [Fact]
    public async Task CreateAsync_NotesOnly_PersistsLongFreeformNoteWithoutCap()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var notes = new string('x', 5000);
        var log = NewLog(userId, new DateOnly(2026, 6, 3));
        log.Notes = notes;

        // Act
        await CreateAsync(log, ct);
        var actual = await GetByIdAsync(userId, log.WorkoutLogId, ct);

        // Assert
        actual.Should().NotBeNull();
        actual!.Notes.Should().Be(notes);
        actual.Notes!.Length.Should().Be(5000);
        actual.Metrics.Should().BeNull();
        actual.Splits.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_OffPlan_PersistsWithNullPrescription()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var log = NewLog(userId, new DateOnly(2026, 6, 4));

        // Act — Prescription left unset (off-plan).
        await CreateAsync(log, ct);
        var actual = await GetByIdAsync(userId, log.WorkoutLogId, ct);

        // Assert
        actual.Should().NotBeNull();
        actual!.Prescription.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_OnPlan_PersistsPrescriptionSnapshotInRealColumns()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        var log = NewLog(userId, new DateOnly(2026, 6, 5));
        log.Prescription = new WorkoutPrescriptionSnapshot
        {
            SourcePlanId = planId,
            WeekNumber = 2,
            DayOfWeek = 4,
            WorkoutType = WorkoutType.Tempo,
            PrescribedDistance = Distance.FromKilometers(10.0),
            PrescribedDuration = Duration.FromMinutes(50.0),
            PrescribedPaceFast = Pace.FromSecondsPerKm(280.0),
            PrescribedPaceSlow = Pace.FromSecondsPerKm(330.0),
        };

        // Act
        await CreateAsync(log, ct);
        var matches = await GetByPlannedWorkoutAsync(userId, planId, 2, 4, ct);

        // Assert — the coordinate query returns it, and value objects round-trip.
        matches.Should().ContainSingle();
        var prescription = matches[0].Prescription;
        prescription.Should().NotBeNull();
        prescription!.SourcePlanId.Should().Be(planId);
        prescription.WorkoutType.Should().Be(WorkoutType.Tempo);
        prescription.PrescribedDistance.Meters.Should().Be(10000.0);
        prescription.PrescribedDuration.TotalMinutes.Should().Be(50.0);
        prescription.PrescribedPace.Fast.SecondsPerKm.Should().Be(280.0);
        prescription.PrescribedPace.Slow.SecondsPerKm.Should().Be(330.0);
    }

    [Fact]
    public async Task GetByPlannedWorkout_ExcludesOffPlanLogs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 6)), ct); // off-plan (null prescription)

        // Act
        var matches = await GetByPlannedWorkoutAsync(userId, planId, 2, 4, ct);

        // Assert — a null-prescription row must never match a coordinate query.
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ScopesToOwningUser_OtherUserGetsNull()
    {
        // Arrange — owner's log, plus a second unrelated user.
        var ct = TestContext.Current.CancellationToken;
        var ownerId = await SeedUserAsync();
        var otherId = await SeedUserAsync();
        var log = NewLog(ownerId, new DateOnly(2026, 6, 11));
        await CreateAsync(log, ct);

        // Act
        var asOwner = await GetByIdAsync(ownerId, log.WorkoutLogId, ct);
        var asOther = await GetByIdAsync(otherId, log.WorkoutLogId, ct);

        // Assert — the owner sees it; another user is scoped out even with the real id.
        asOwner.Should().NotBeNull();
        asOther.Should().BeNull();
    }

    [Fact]
    public async Task GetByPlannedWorkout_ScopesToOwningUser_OtherUserGetsEmpty()
    {
        // Arrange — owner's on-plan log at a coordinate, plus a second user.
        var ct = TestContext.Current.CancellationToken;
        var ownerId = await SeedUserAsync();
        var otherId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        var log = NewLog(ownerId, new DateOnly(2026, 6, 12));
        log.Prescription = new WorkoutPrescriptionSnapshot
        {
            SourcePlanId = planId,
            WeekNumber = 3,
            DayOfWeek = 2,
            WorkoutType = WorkoutType.Tempo,
            PrescribedDistance = Distance.FromKilometers(8.0),
            PrescribedDuration = Duration.FromMinutes(40.0),
            PrescribedPaceFast = Pace.FromSecondsPerKm(280.0),
            PrescribedPaceSlow = Pace.FromSecondsPerKm(330.0),
        };
        await CreateAsync(log, ct);

        // Act — same coordinate, queried as each user.
        var asOwner = await GetByPlannedWorkoutAsync(ownerId, planId, 3, 2, ct);
        var asOther = await GetByPlannedWorkoutAsync(otherId, planId, 3, 2, ct);

        // Assert — the coordinate query never crosses the user boundary.
        asOwner.Should().ContainSingle();
        asOther.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUser_ReturnsLogsNewestFirstByKeyset()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 1)), ct);
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 8)), ct);
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 15)), ct);

        // Act
        var page = await GetByUserAsync(userId, cursor: null, limit: 10, ct);

        // Assert
        page.Select(w => w.OccurredOn).Should().Equal(
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 6, 8),
            new DateOnly(2026, 6, 1));
    }

    [Fact]
    public async Task GetByUser_KeysetCursor_PagesAcrossBoundaryWithoutSkipsOrDuplicates()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 1)), ct);
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 8)), ct);
        await CreateAsync(NewLog(userId, new DateOnly(2026, 6, 15)), ct);

        // Act — first page, then resume from its tail via a keyset cursor.
        var page1 = await GetByUserAsync(userId, cursor: null, limit: 2, ct);
        var tail = page1[^1];
        var cursor = new WorkoutLogCursor(tail.OccurredOn, tail.WorkoutLogId);
        var page2 = await GetByUserAsync(userId, cursor, limit: 2, ct);

        // Assert — contiguous newest-first order, no skips and no duplicates.
        page1.Select(w => w.OccurredOn).Should().Equal(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 8));
        page2.Select(w => w.OccurredOn).Should().Equal(new DateOnly(2026, 6, 1));
        page1.Select(w => w.WorkoutLogId).Should().NotIntersectWith(page2.Select(w => w.WorkoutLogId));
    }

    [Theory]
    [InlineData(CompletionStatus.Partial)]
    [InlineData(CompletionStatus.Skipped)]
    public async Task CreateAsync_NonDefaultCompletionStatus_RoundTrips(CompletionStatus status)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var log = NewLog(userId, new DateOnly(2026, 6, 9), status);

        // Act
        await CreateAsync(log, ct);
        var actual = await GetByIdAsync(userId, log.WorkoutLogId, ct);

        // Assert — the non-zero enum values survive the integer column round-trip.
        actual.Should().NotBeNull();
        actual!.CompletionStatus.Should().Be(status);
    }

    [Fact]
    public async Task GetByUser_KeysetCursor_SameDate_PagesViaIdTiebreakWithoutSkipsOrDuplicates()
    {
        // Arrange — three logs on the SAME day force the OccurredOn tie, so paging
        // falls entirely to the WorkoutLogId tiebreak arm of the keyset predicate.
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var sameDate = new DateOnly(2026, 6, 10);
        await CreateAsync(NewLog(userId, sameDate), ct);
        await CreateAsync(NewLog(userId, sameDate), ct);
        await CreateAsync(NewLog(userId, sameDate), ct);

        // Act
        var page1 = await GetByUserAsync(userId, cursor: null, limit: 2, ct);
        var tail = page1[^1];
        var cursor = new WorkoutLogCursor(tail.OccurredOn, tail.WorkoutLogId);
        var page2 = await GetByUserAsync(userId, cursor, limit: 2, ct);

        // Assert — clean partition: 2 + 1 distinct rows, no skips, no duplicates.
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(1);
        page1.Select(w => w.WorkoutLogId).Should().NotIntersectWith(page2.Select(w => w.WorkoutLogId));
        page1.Concat(page2).Select(w => w.WorkoutLogId).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [Fact]
    public async Task GetByUser_ScopesToOwningUser_ExcludesOtherUsersLogs()
    {
        // Arrange — two users, with an overlapping OccurredOn date to rule out any
        // date-based separation masking a missing user filter.
        var ct = TestContext.Current.CancellationToken;
        var ownerId = await SeedUserAsync();
        var otherId = await SeedUserAsync();
        var ownerOlder = NewLog(ownerId, new DateOnly(2026, 6, 1));
        var ownerNewer = NewLog(ownerId, new DateOnly(2026, 6, 8));
        var otherSameDate = NewLog(otherId, new DateOnly(2026, 6, 8)); // shares ownerNewer's date
        await CreateAsync(ownerOlder, ct);
        await CreateAsync(ownerNewer, ct);
        await CreateAsync(otherSameDate, ct);

        // Act
        var page = await GetByUserAsync(ownerId, cursor: null, limit: 10, ct);

        // Assert — exactly the owner's two logs; the other user's log never leaks in.
        page.Select(w => w.WorkoutLogId).Should().BeEquivalentTo(
            new[] { ownerNewer.WorkoutLogId, ownerOlder.WorkoutLogId });
        page.Should().OnlyContain(w => w.UserId == ownerId);
    }

    [Fact]
    public async Task GetByPlannedWorkout_FiltersOnWeekAndDay_ExcludesOtherCoordinates()
    {
        // Arrange — same user and plan, two distinct (week, day) coordinates, so the
        // WeekNumber/DayOfWeek equality arms (not just UserId/SourcePlanId) are exercised.
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        var atWeek2Day4 = NewLog(userId, new DateOnly(2026, 6, 13));
        atWeek2Day4.Prescription = NewPrescription(planId, weekNumber: 2, dayOfWeek: 4);
        var atWeek3Day1 = NewLog(userId, new DateOnly(2026, 6, 14));
        atWeek3Day1.Prescription = NewPrescription(planId, weekNumber: 3, dayOfWeek: 1);
        await CreateAsync(atWeek2Day4, ct);
        await CreateAsync(atWeek3Day1, ct);

        // Act — query the (2, 4) coordinate only.
        var matches = await GetByPlannedWorkoutAsync(userId, planId, 2, 4, ct);

        // Assert — the (3, 1) log is excluded; a predicate matching only on
        // (UserId, SourcePlanId) would wrongly return both.
        matches.Should().ContainSingle()
            .Which.WorkoutLogId.Should().Be(atWeek2Day4.WorkoutLogId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByUser_NonPositiveLimit_ThrowsArgumentOutOfRange(int limit)
    {
        // Arrange — the guard runs before any query, so no seeded data is needed.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var act = () => GetByUserAsync(Guid.NewGuid(), cursor: null, limit, ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(limit));
    }

    private static WorkoutLog NewLog(
        Guid userId, DateOnly occurredOn, CompletionStatus status = CompletionStatus.Complete)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkoutLog
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = userId,
            TenantId = userId.ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = occurredOn,
            Distance = Distance.FromMeters(5000.0),
            Duration = Duration.FromMinutes(25.0),
            CompletionStatus = status,
            CreatedOn = now,
            ModifiedOn = now,
        };
    }

    private static WorkoutPrescriptionSnapshot NewPrescription(
        Guid sourcePlanId, int weekNumber, int dayOfWeek) =>
        new()
        {
            SourcePlanId = sourcePlanId,
            WeekNumber = weekNumber,
            DayOfWeek = dayOfWeek,
            WorkoutType = WorkoutType.Tempo,
            PrescribedDistance = Distance.FromKilometers(10.0),
            PrescribedDuration = Duration.FromMinutes(50.0),
            PrescribedPaceFast = Pace.FromSecondsPerKm(280.0),
            PrescribedPaceSlow = Pace.FromSecondsPerKm(330.0),
        };

    private async Task CreateAsync(WorkoutLog log, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        await repo.CreateAsync(log, ct);
    }

    private async Task<WorkoutLog?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByIdAsync(userId, id, ct);
    }

    private async Task<IReadOnlyList<WorkoutLog>> GetByUserAsync(
        Guid userId, WorkoutLogCursor? cursor, int limit, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByUserAsync(userId, cursor, limit, ct);
    }

    private async Task<IReadOnlyList<WorkoutLog>> GetByPlannedWorkoutAsync(
        Guid userId, Guid sourcePlanId, int weekNumber, int dayOfWeek, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByPlannedWorkoutAsync(userId, sourcePlanId, weekNumber, dayOfWeek, ct);
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"workoutlog-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, "Str0ngTestPassw0rd!");
        result.Succeeded.Should().BeTrue(
            because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }
}
