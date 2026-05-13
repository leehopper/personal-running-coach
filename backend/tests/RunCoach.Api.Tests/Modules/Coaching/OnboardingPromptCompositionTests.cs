using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Behaviour guard for <see cref="OnboardingPromptComposition.Neutralized"/>.
/// The flag is computed from <see cref="OnboardingPromptComposition.Findings"/>
/// so the audit trail and the boolean cannot drift — a regression that would
/// re-introduce a stored field is caught here.
/// </summary>
public sealed class OnboardingPromptCompositionTests
{
    [Fact]
    public void Neutralized_IsComputedFromFindings_TrueWhenAnyFindingStripped()
    {
        // Arrange — a single Tier 1 finding with Stripped=true is enough.
        var findings = ImmutableArray.Create(
            new SanitizationFinding(
                Category: SanitizationCategory.UnicodeTag,
                PatternId: "U-TAGS",
                OriginalLength: 10,
                SanitizedLength: 6,
                Stripped: true));

        // Act
        var actual = new OnboardingPromptComposition(
            SystemPrompt: "system",
            UserMessage: "user",
            Findings: findings);

        // Assert
        actual.Neutralized.Should().BeTrue(
            because: "Neutralized must reflect any finding with Stripped=true");
    }

    [Fact]
    public void Neutralized_IsComputedFromFindings_FalseWhenAllFindingsLogOnly()
    {
        // Arrange — log-only findings (Stripped=false) must NOT flip Neutralized.
        var findings = ImmutableArray.Create(
            new SanitizationFinding(
                Category: SanitizationCategory.RegexHitDirectOverride,
                PatternId: "PI-01",
                OriginalLength: 20,
                SanitizedLength: 20,
                Stripped: false));

        // Act
        var actual = new OnboardingPromptComposition(
            SystemPrompt: "system",
            UserMessage: "user",
            Findings: findings);

        // Assert
        actual.Neutralized.Should().BeFalse(
            because: "log-only findings carry audit information without modifying the input");
    }

    [Fact]
    public void Neutralized_IsComputedFromFindings_FalseWhenFindingsEmpty()
    {
        // Arrange + Act — empty findings means clean input.
        var actual = new OnboardingPromptComposition(
            SystemPrompt: "system",
            UserMessage: "user",
            Findings: ImmutableArray<SanitizationFinding>.Empty);

        // Assert
        actual.Neutralized.Should().BeFalse(
            because: "an empty findings list cannot have produced any neutralization");
    }

    [Fact]
    public void Neutralized_RemainsInSyncOnWithExpression()
    {
        // Arrange — start with no findings (Neutralized = false), then `with` a
        // finding that strips: the recomputation must surface the new value
        // instead of carrying a stale boolean from the previous record.
        var initial = new OnboardingPromptComposition(
            SystemPrompt: "system",
            UserMessage: "user",
            Findings: ImmutableArray<SanitizationFinding>.Empty);

        var expectedInitial = false;
        initial.Neutralized.Should().Be(expectedInitial);

        // Act
        var actual = initial with
        {
            Findings = ImmutableArray.Create(
                new SanitizationFinding(
                    Category: SanitizationCategory.ZeroWidth,
                    PatternId: "U-ZW",
                    OriginalLength: 5,
                    SanitizedLength: 4,
                    Stripped: true)),
        };

        // Assert
        actual.Neutralized.Should().BeTrue(
            because: "a `with` expression that swaps in stripping findings must flip Neutralized");
    }
}
