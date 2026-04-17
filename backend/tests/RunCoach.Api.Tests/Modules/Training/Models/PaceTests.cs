using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class PaceTests
{
    [Fact]
    public void FromSecondsPerKm_StoresValue()
    {
        var pace = Pace.FromSecondsPerKm(300.0);

        pace.SecondsPerKm.Should().Be(300.0);
    }

    [Fact]
    public void FromSecondsPerKm_ZeroValue_ThrowsArgumentOutOfRangeException()
    {
        var act = () => Pace.FromSecondsPerKm(0.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromSecondsPerKm_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var act = () => Pace.FromSecondsPerKm(-1.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromTimeSpan_RoundTrip_PreservesSeconds()
    {
        var timePerKm = TimeSpan.FromSeconds(360.0);

        var pace = Pace.FromTimeSpan(timePerKm);
        var roundTripped = pace.ToTimeSpan();

        roundTripped.TotalSeconds.Should().BeApproximately(
            360.0,
            1e-10);
    }

    [Fact]
    public void IsFasterThan_LowerSecondsPerKm_ReturnsTrue()
    {
        var faster = Pace.FromSecondsPerKm(300.0);
        var slower = Pace.FromSecondsPerKm(360.0);

        faster.IsFasterThan(slower).Should().BeTrue();
    }

    [Fact]
    public void IsFasterThan_HigherSecondsPerKm_ReturnsFalse()
    {
        var slower = Pace.FromSecondsPerKm(360.0);
        var faster = Pace.FromSecondsPerKm(300.0);

        slower.IsFasterThan(faster).Should().BeFalse();
    }

    [Fact]
    public void IsFasterThan_EqualSecondsPerKm_ReturnsFalse()
    {
        var pace = Pace.FromSecondsPerKm(300.0);

        pace.IsFasterThan(pace).Should().BeFalse();
    }

    [Fact]
    public void IsSlowerThan_HigherSecondsPerKm_ReturnsTrue()
    {
        var slower = Pace.FromSecondsPerKm(360.0);
        var faster = Pace.FromSecondsPerKm(300.0);

        slower.IsSlowerThan(faster).Should().BeTrue();
    }

    [Fact]
    public void IsSlowerThan_LowerSecondsPerKm_ReturnsFalse()
    {
        var faster = Pace.FromSecondsPerKm(300.0);
        var slower = Pace.FromSecondsPerKm(360.0);

        faster.IsSlowerThan(slower).Should().BeFalse();
    }

    [Fact]
    public void IsSlowerThan_EqualSecondsPerKm_ReturnsFalse()
    {
        var pace = Pace.FromSecondsPerKm(300.0);

        pace.IsSlowerThan(pace).Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameSecondsPerKm_AreEqual()
    {
        var a = Pace.FromSecondsPerKm(300.0);
        var b = Pace.FromSecondsPerKm(300.0);

        a.Should().Be(b);
    }
}
