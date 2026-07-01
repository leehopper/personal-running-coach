using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Settings;

namespace RunCoach.Api.Tests.Modules.Settings;

/// <summary>
/// Unit tests for <see cref="SettingsController"/>'s missing-user-claim reject path
/// (Slice 4C-units). The integration suite covers the authenticated happy/validation
/// paths end-to-end; these pin the defensive branch that a real
/// <c>[Authorize]</c> pipeline never reaches (an authenticated principal whose
/// id claim is absent or malformed) so it returns 401 and touches no state.
/// </summary>
public class SettingsControllerTests
{
    [Fact]
    public async Task GetUnits_NoUserIdClaim_Returns401_WithoutReadingSettings()
    {
        // Arrange — an authenticated principal carrying no NameIdentifier/sub claim.
        var (controller, service) = CreateController(withUserIdClaim: false);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.GetUnits(ct);

        // Assert
        actual.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        await service.DidNotReceiveWithAnyArgs().GetPreferredUnitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutUnits_NoUserIdClaim_Returns401_WithoutWritingSettings()
    {
        // Arrange
        var (controller, service) = CreateController(withUserIdClaim: false);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.PutUnits(new UnitPreferenceDto(PreferredUnits.Miles), ct);

        // Assert
        actual.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        await service.DidNotReceiveWithAnyArgs().SetPreferredUnitsAsync(
            Arg.Any<Guid>(), Arg.Any<PreferredUnits>(), Arg.Any<CancellationToken>());
    }

    private static (SettingsController Controller, IUserSettingsService Service) CreateController(bool withUserIdClaim)
    {
        var service = Substitute.For<IUserSettingsService>();

        Claim[] claims = withUserIdClaim
            ? [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())]
            : [];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

        var controller = new SettingsController(service, NullLogger<SettingsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
            ProblemDetailsFactory = StubProblemDetailsFactory(),
        };

        return (controller, service);
    }

    private static ProblemDetailsFactory StubProblemDetailsFactory()
    {
        // ControllerBase.Problem resolves a ProblemDetailsFactory; outside a real
        // host none is registered, so the reject path gets a minimal pass-through stub.
        var factory = Substitute.For<ProblemDetailsFactory>();
        factory.CreateProblemDetails(
                Arg.Any<HttpContext>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns(ci => new ProblemDetails
            {
                Status = ci.ArgAt<int?>(1),
                Title = ci.ArgAt<string?>(2),
                Type = ci.ArgAt<string?>(3),
                Detail = ci.ArgAt<string?>(4),
                Instance = ci.ArgAt<string?>(5),
            });
        return factory;
    }
}
