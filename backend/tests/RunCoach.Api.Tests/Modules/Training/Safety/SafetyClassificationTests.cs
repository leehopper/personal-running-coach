using System.Reflection;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Training.Safety;

/// <summary>
/// Guards the construction invariants of <see cref="SafetyClassification"/>: a
/// Green result never carries a referral category, a non-Green result always
/// does, and the only public entry points are the factories (mirrors
/// <c>PlanAdaptationOutputValidationResult</c>).
/// </summary>
public sealed class SafetyClassificationTests
{
    [Fact]
    public void Green_HasGreenTierAndNoCategory()
    {
        // Act
        var actual = SafetyClassification.Green();

        // Assert
        actual.Tier.Should().Be(SafetyTier.Green);
        actual.Category.Should().Be(ReferralCategory.None);
    }

    [Theory]
    [InlineData(ReferralCategory.Crisis)]
    [InlineData(ReferralCategory.EmergencyReferral)]
    public void Red_CarriesRedTierAndCategory(ReferralCategory category)
    {
        // Act
        var actual = SafetyClassification.Red(category);

        // Assert
        actual.Tier.Should().Be(SafetyTier.Red);
        actual.Category.Should().Be(category);
    }

    [Theory]
    [InlineData(ReferralCategory.Injury)]
    [InlineData(ReferralCategory.RedS)]
    public void Amber_CarriesAmberTierAndCategory(ReferralCategory category)
    {
        // Act
        var actual = SafetyClassification.Amber(category);

        // Assert
        actual.Tier.Should().Be(SafetyTier.Amber);
        actual.Category.Should().Be(category);
    }

    [Fact]
    public void Red_Throws_WhenCategoryIsNone()
    {
        // Act
        var act = () => SafetyClassification.Red(ReferralCategory.None);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Amber_Throws_WhenCategoryIsNone()
    {
        // Act
        var act = () => SafetyClassification.Amber(ReferralCategory.None);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ReferralCategory.Injury)]
    [InlineData(ReferralCategory.RedS)]
    public void Red_Throws_WhenCategoryIsAmberAligned(ReferralCategory category)
    {
        // Act
        var act = () => SafetyClassification.Red(category);

        // Assert — Red is reserved for `Crisis`/`EmergencyReferral`; an
        // Amber-aligned category must not construct a Red classification.
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ReferralCategory.Crisis)]
    [InlineData(ReferralCategory.EmergencyReferral)]
    public void Amber_Throws_WhenCategoryIsRedAligned(ReferralCategory category)
    {
        // Act
        var act = () => SafetyClassification.Amber(category);

        // Assert — Amber is reserved for `Injury`/`RedS`; a Red-aligned category
        // must not construct an Amber classification.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_IsByTierAndCategory()
    {
        // Assert
        SafetyClassification.Red(ReferralCategory.Crisis)
            .Should().Be(SafetyClassification.Red(ReferralCategory.Crisis));
        SafetyClassification.Red(ReferralCategory.Crisis)
            .Should().NotBe(SafetyClassification.Amber(ReferralCategory.Injury));
    }

    [Fact]
    public void PrimaryCtor_NotPubliclyConstructable()
    {
        // Arrange — the raw (tier, category) constructor must be internal so the
        // factories are the only public entry points and contradictory states
        // cannot be expressed.
        var ctors = typeof(SafetyClassification)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(c => c.GetParameters().Length > 0)
            .ToArray();

        // Assert
        ctors.Should().NotBeEmpty();
        ctors.Should().OnlyContain(c => !c.IsPublic);
    }
}
