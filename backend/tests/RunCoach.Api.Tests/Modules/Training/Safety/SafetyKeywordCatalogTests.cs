using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Training.Safety;

/// <summary>
/// Guards the versioned high-risk keyword resource: it is versioned, non-empty,
/// every rule maps to a defined tier + matching category, ids are unique, and
/// rules are grouped in escalation-precedence order so first-match yields the
/// highest-precedence classification.
/// </summary>
public sealed class SafetyKeywordCatalogTests
{
    private static readonly SafetyKeywordCatalog Catalog = SafetyKeywordCatalog.Default;

    [Fact]
    public void PolicyVersion_IsPresent()
    {
        SafetyKeywordCatalog.PolicyVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Default_HasRules()
    {
        Catalog.Rules.Should().NotBeEmpty();
    }

    [Fact]
    public void EveryRule_HasNonGreenTierAndAMatchingCategory()
    {
        // Assert — no Green rules; Red rules carry crisis/emergency, Amber rules
        // carry injury/RED-S; no rule carries ReferralCategory.None.
        Catalog.Rules.Should().OnlyContain(r => r.Tier != SafetyTier.Green);
        Catalog.Rules.Should().OnlyContain(r => r.Category != ReferralCategory.None);
        Catalog.Rules
            .Where(r => r.Tier == SafetyTier.Red)
            .Should().OnlyContain(r =>
                r.Category == ReferralCategory.Crisis || r.Category == ReferralCategory.EmergencyReferral);
        Catalog.Rules
            .Where(r => r.Tier == SafetyTier.Amber)
            .Should().OnlyContain(r =>
                r.Category == ReferralCategory.Injury || r.Category == ReferralCategory.RedS);
    }

    [Fact]
    public void RuleIds_AreUnique()
    {
        Catalog.Rules.Select(r => r.RuleId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryCategory_HasAtLeastOneRule()
    {
        var categories = Catalog.Rules.Select(r => r.Category).Distinct();
        categories.Should().BeEquivalentTo(new[]
        {
            ReferralCategory.Crisis,
            ReferralCategory.EmergencyReferral,
            ReferralCategory.Injury,
            ReferralCategory.RedS,
        });
    }

    [Fact]
    public void Rules_AreGroupedInEscalationPrecedenceOrder()
    {
        // Assert — first appearance of each category follows crisis → emergency
        // → injury → RED-S, so SafetyGate's first-match returns the highest tier.
        var firstAppearance = Catalog.Rules.Select(r => r.Category).Distinct().ToList();
        firstAppearance.Should().Equal(
            ReferralCategory.Crisis,
            ReferralCategory.EmergencyReferral,
            ReferralCategory.Injury,
            ReferralCategory.RedS);
    }

    [Fact]
    public void ForTesting_BacksTheCatalogWithTheGivenRules()
    {
        // Arrange
        var rules = ImmutableArray.Create(
            new SafetyKeywordCatalog.SafetyRule(
                "T-01",
                SafetyTier.Red,
                ReferralCategory.Crisis,
                new System.Text.RegularExpressions.Regex("x")));

        // Act
        var catalog = SafetyKeywordCatalog.ForTesting(rules);

        // Assert
        catalog.Rules.Should().ContainSingle().Which.RuleId.Should().Be("T-01");
    }
}
