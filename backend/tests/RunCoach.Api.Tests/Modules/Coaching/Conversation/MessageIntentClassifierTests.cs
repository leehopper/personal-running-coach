using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="MessageIntentClassifier"/> — the orchestration that composes
/// the classifier prompt, runs the Pattern-B Haiku triage on the per-call model override, and
/// enforces the slot-matches-discriminator invariant (coercing a reject to Ambiguous). The
/// LLM is stubbed; this tier makes no real call.
/// </summary>
public sealed class MessageIntentClassifierTests
{
    private const string ClassifierModel = "claude-haiku-4-5";
    private static readonly DateOnly Today = new(2026, 6, 26);

    private readonly IContextAssembler _contextAssembler = Substitute.For<IContextAssembler>();
    private readonly ICoachingLlm _coachingLlm = Substitute.For<ICoachingLlm>();

    public MessageIntentClassifierTests()
    {
        _contextAssembler
            .ComposeForClassificationAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClassificationPromptComposition(
                "classifier system",
                "classifier user",
                ImmutableArray<SanitizationFinding>.Empty)));
    }

    [Fact]
    public async Task ClassifyAsync_ComposesClassifierPrompt_AndReturnsValidQuestion()
    {
        // Arrange
        StubLlm(new MessageIntentOutput { Intent = MessageIntent.Question, WorkoutLog = null });
        var sut = CreateSut();

        // Act
        var result = await sut.ClassifyAsync(Today, "how's my pace looking", TestContext.Current.CancellationToken);

        // Assert
        result.Intent.Should().Be(MessageIntent.Question);
        result.WorkoutLog.Should().BeNull();
        await _contextAssembler.Received(1).ComposeForClassificationAsync(
            Today, "how's my pace looking", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyAsync_TargetsClassifierModel_AndPassesFrozenSchema()
    {
        // Arrange
        StubLlm(new MessageIntentOutput { Intent = MessageIntent.Question, WorkoutLog = null });
        var sut = CreateSut();

        // Act
        await sut.ClassifyAsync(Today, "any question", TestContext.Current.CancellationToken);

        // Assert — the classifier targets the Haiku binding via the model override and ships
        // the byte-stable frozen schema (never the null-fallback path).
        await _coachingLlm.Received(1).GenerateStructuredAsync<MessageIntentOutput>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, JsonElement>?>(s => ReferenceEquals(s, ClassifierSchema.Frozen)),
            Arg.Any<CacheControl?>(),
            ClassifierModel,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsWorkoutLog_WithDraft()
    {
        // Arrange
        var draft = new StructuredLogDraft
        {
            OccurredOn = Today,
            DistanceMeters = 5000,
            DurationSeconds = 1500,
            CompletionStatus = CompletionStatus.Complete,
            Notes = null,
        };
        StubLlm(new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = draft });
        var sut = CreateSut();

        // Act
        var result = await sut.ClassifyAsync(Today, "ran 5k in 25 min", TestContext.Current.CancellationToken);

        // Assert
        result.Intent.Should().Be(MessageIntent.WorkoutLog);
        result.WorkoutLog.Should().BeSameAs(draft);
    }

    [Fact]
    public async Task ClassifyAsync_CoercesToAmbiguous_WhenValidatorRejects()
    {
        // Arrange — a WorkoutLog intent with a null draft is a structurally-invalid union.
        StubLlm(new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = null });
        var sut = CreateSut();

        // Act
        var result = await sut.ClassifyAsync(Today, "did a thing", TestContext.Current.CancellationToken);

        // Assert — coerced to Ambiguous (ask, never guess); never returns the invalid union.
        result.Intent.Should().Be(MessageIntent.Ambiguous);
        result.WorkoutLog.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_PropagatesPermanentCoachingLlmException()
    {
        // Arrange — the DEC-073 totality contract: a call failure surfaces, not swallowed.
        _coachingLlm
            .GenerateStructuredAsync<MessageIntentOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<(MessageIntentOutput, AnthropicUsage)>>(_ =>
                throw new PermanentCoachingLlmException("rejected", innerException: null));
        var sut = CreateSut();

        // Act
        var act = () => sut.ClassifyAsync(Today, "anything", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PermanentCoachingLlmException>();
    }

    [Fact]
    public async Task ClassifyAsync_ThrowsArgumentNullException_WhenMessageIsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.ClassifyAsync(Today, null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private void StubLlm(MessageIntentOutput output)
    {
        _coachingLlm
            .GenerateStructuredAsync<MessageIntentOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>?>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((output, AnthropicUsage.Zero)));
    }

    private MessageIntentClassifier CreateSut() => new(
        _contextAssembler,
        _coachingLlm,
        Options.Create(new CoachingLlmSettings { ClassifierModelId = ClassifierModel }),
        NullLogger<MessageIntentClassifier>.Instance);
}
