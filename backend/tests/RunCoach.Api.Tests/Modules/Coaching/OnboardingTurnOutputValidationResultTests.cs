using System.Linq;
using System.Reflection;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Guards for <see cref="OnboardingTurnOutputValidationResult"/>'s factory-only
/// construction contract: production callers go through <see cref="OnboardingTurnOutputValidationResult.Valid"/>
/// and <see cref="OnboardingTurnOutputValidationResult.Invalid"/>, and the raw
/// three-field constructor is <c>internal</c> so contradictory triples cannot
/// reach production from outside this assembly.
/// </summary>
public sealed class OnboardingTurnOutputValidationResultTests
{
    [Fact]
    public void PrimaryCtor_NotPubliclyConstructable()
    {
        // Arrange + Act — reflect the type's instance constructors and inspect
        // their declared accessibility.
        var ctors = typeof(OnboardingTurnOutputValidationResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(c => c.GetParameters().Length > 0)
            .ToArray();

        // Assert — at least one parameterised ctor exists, and none of them
        // are public. This catches a regression that would re-introduce a
        // public positional record constructor.
        ctors.Should().NotBeEmpty(
            because: "the type must expose a non-public ctor that Valid/Invalid delegate to");

        ctors.Should().OnlyContain(
            c => !c.IsPublic,
            because: "the raw three-field constructor must be internal so factory methods are the only public entry points");
    }

    [Fact]
    public void Valid_ReturnsValidResultWithNoneViolation()
    {
        // Act
        var actual = OnboardingTurnOutputValidationResult.Valid();

        // Assert
        actual.IsValid.Should().BeTrue();
        actual.Violation.Should().Be(OnboardingTurnOutputValidationViolation.None);
        actual.NonNullSlotCount.Should().Be(0);
    }

    [Fact]
    public void Valid_PropagatesNonNullSlotCount()
    {
        // Arrange
        const int expectedCount = 1;

        // Act
        var actual = OnboardingTurnOutputValidationResult.Valid(nonNullSlotCount: expectedCount);

        // Assert
        actual.IsValid.Should().BeTrue();
        actual.NonNullSlotCount.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData(OnboardingTurnOutputValidationViolation.NoNormalizedSlot)]
    [InlineData(OnboardingTurnOutputValidationViolation.MultipleNormalizedSlots)]
    [InlineData(OnboardingTurnOutputValidationViolation.SlotTopicMismatch)]
    [InlineData(OnboardingTurnOutputValidationViolation.ClarificationWithoutReason)]
    [InlineData(OnboardingTurnOutputValidationViolation.ContentBlockShape)]
    public void Invalid_ReturnsInvalidResultForEachViolation(
        OnboardingTurnOutputValidationViolation violation)
    {
        // Act
        var actual = OnboardingTurnOutputValidationResult.Invalid(violation);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(violation);
    }

    [Fact]
    public void Invalid_PropagatesNonNullSlotCount()
    {
        // Arrange
        const int expectedCount = 2;

        // Act
        var actual = OnboardingTurnOutputValidationResult.Invalid(
            OnboardingTurnOutputValidationViolation.MultipleNormalizedSlots,
            nonNullSlotCount: expectedCount);

        // Assert
        actual.NonNullSlotCount.Should().Be(expectedCount);
    }

    [Fact]
    public void Invalid_RejectsNoneViolationAsContradiction()
    {
        // Act
        var act = () => OnboardingTurnOutputValidationResult.Invalid(
            OnboardingTurnOutputValidationViolation.None);

        // Assert — a valid-with-no-violation is the well-formed case; Invalid
        // must reject it so the result type cannot express the contradiction.
        act.Should().Throw<ArgumentException>()
            .WithParameterName("violation");
    }

    [Fact]
    public void Valid_NegativeCount_Throws()
    {
        var act = () => OnboardingTurnOutputValidationResult.Valid(-1);
        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("nonNullSlotCount");
    }

    [Fact]
    public void Invalid_NegativeCount_Throws()
    {
        var act = () => OnboardingTurnOutputValidationResult.Invalid(
            OnboardingTurnOutputValidationViolation.MultipleNormalizedSlots, -1);
        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("nonNullSlotCount");
    }

    [Fact]
    public void Default_OfReferenceTypeIsNull()
    {
        OnboardingTurnOutputValidationResult? actual = default;
        actual.Should().BeNull();
    }
}
