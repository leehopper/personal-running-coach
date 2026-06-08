using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Training.Safety;

/// <summary>
/// Guards the scripted Red-crisis content (Slice 3 Unit 3 / DEC-079): the two
/// exact crisis-resource strings are present verbatim, the content is versioned,
/// and it carries no trademarked "VDOT" term (user-facing copy).
/// </summary>
public sealed class CrisisResponseContentTests
{
    [Fact]
    public void CrisisResponse_ContainsTheExact988LifelineString()
    {
        CrisisResponseContent.CrisisResponse.Should().Contain("988 Suicide & Crisis Lifeline");
    }

    [Fact]
    public void CrisisResponse_ContainsTheExactCrisisTextLineString()
    {
        CrisisResponseContent.CrisisResponse.Should().Contain("Crisis Text Line: text 741741");
    }

    [Fact]
    public void ContentVersion_IsPresent()
    {
        CrisisResponseContent.ContentVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CrisisResponse_DoesNotContainTrademarkedTerm()
    {
        // Trademark rule: user-facing copy must never contain "VDOT" (case-insensitive).
        CrisisResponseContent.CrisisResponse.Should().NotContainEquivalentOf("vdot");
    }
}
