using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RunCoach.Api.Modules.Training.Plan;
using Xunit;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

public sealed class LocalDateProviderTests
{
    [Fact]
    public void Today_LateEveningLocalButNextDayUtc_ReturnsLocalCalendarDay()
    {
        // Arrange — 2026-06-12 23:30 America/New_York (UTC-4 in June) is
        // 2026-06-13 03:30 UTC. The local wall-calendar day is the 12th.
        var utcNow = new DateTimeOffset(2026, 6, 13, 3, 30, 0, TimeSpan.Zero);
        var sut = CreateSut(utcNow, "America/New_York");

        // Act
        var actual = sut.Today();

        // Assert
        actual.Should().Be(new DateOnly(2026, 6, 12));
        actual.Should().NotBe(new DateOnly(2026, 6, 13), "the UTC day must not leak through");
    }

    [Fact]
    public void Today_MiddayUtc_MatchesLocalDay()
    {
        var utcNow = new DateTimeOffset(2026, 6, 12, 16, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(utcNow, "America/New_York");

        sut.Today().Should().Be(new DateOnly(2026, 6, 12));
    }

    private static LocalDateProvider CreateSut(DateTimeOffset utcNow, string timeZone)
    {
        var time = new FakeTimeProvider(utcNow);
        var options = Options.Create(new AppClockSettings { TimeZone = timeZone });
        return new LocalDateProvider(time, options);
    }
}
