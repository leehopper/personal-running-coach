using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for <see cref="WorkoutLogService.QueryAsync"/> page-size clamping
/// (slice-2b Unit 4 / PR4). The clamp is load-bearing: its floor keeps a
/// non-positive client limit from reaching the repository's
/// <c>ThrowIfNegativeOrZero</c> guard (which would surface as an unhandled 500),
/// and its ceiling is the only bound on the fetch size (the request DTO carries no
/// range validation). Each test asserts the exact limit handed to the repository.
/// </summary>
public class WorkoutLogServiceTests
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [Fact]
    public async Task QueryAsync_NullLimit_AppliesDefaultPageSize()
    {
        // Arrange
        var (service, repository) = CreateService();
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await service.QueryAsync(userId, cursor: null, requestedLimit: null, ct);

        // Assert
        await repository.Received(1).GetByUserAsync(
            userId, Arg.Any<WorkoutLogCursor?>(), DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task QueryAsync_NonPositiveLimit_ClampsToOne(int requestedLimit)
    {
        // Arrange
        var (service, repository) = CreateService();
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await service.QueryAsync(userId, cursor: null, requestedLimit, ct);

        // Assert — clamped above the repo's ThrowIfNegativeOrZero guard.
        await repository.Received(1).GetByUserAsync(
            userId, Arg.Any<WorkoutLogCursor?>(), 1, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(101)]
    [InlineData(100_000)]
    [InlineData(int.MaxValue)]
    public async Task QueryAsync_LimitAboveMax_ClampsToMax(int requestedLimit)
    {
        // Arrange
        var (service, repository) = CreateService();
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await service.QueryAsync(userId, cursor: null, requestedLimit, ct);

        // Assert — the ceiling bounds the fetch the DTO does not.
        await repository.Received(1).GetByUserAsync(
            userId, Arg.Any<WorkoutLogCursor?>(), MaxPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_LimitWithinBounds_PassesThrough()
    {
        // Arrange
        var (service, repository) = CreateService();
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await service.QueryAsync(userId, cursor: null, requestedLimit: 50, ct);

        // Assert
        await repository.Received(1).GetByUserAsync(
            userId, Arg.Any<WorkoutLogCursor?>(), 50, Arg.Any<CancellationToken>());
    }

    private static (WorkoutLogService Service, IWorkoutLogRepository Repository) CreateService()
    {
        var repository = Substitute.For<IWorkoutLogRepository>();
        repository
            .GetByUserAsync(Arg.Any<Guid>(), Arg.Any<WorkoutLogCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkoutLog>>(new List<WorkoutLog>()));

        // QueryAsync touches only the repository; the DbContext, Marten store, and
        // TimeProvider serve CreateAsync's prescription resolution and are unused here.
        var service = new WorkoutLogService(
            db: null!,
            repository: repository,
            documentStore: Substitute.For<IDocumentStore>(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<WorkoutLogService>.Instance);
        return (service, repository);
    }
}
