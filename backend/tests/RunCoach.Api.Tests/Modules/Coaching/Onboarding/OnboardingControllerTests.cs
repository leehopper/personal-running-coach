using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Unit tests for <see cref="OnboardingController"/> covering payload validation
/// on the <c>POST /api/v1/onboarding/answers/revise</c> endpoint (Task #142)
/// and the <c>[JsonRequired]</c> annotations on both request DTOs
/// (Tasks #143, #145).
/// </summary>
public sealed class OnboardingControllerTests
{
    private static readonly Guid UserId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Malformed payload (a JSON primitive rather than an object) cannot be
    /// deserialized as <see cref="PrimaryGoalAnswer"/> — the endpoint must
    /// return 400 and must not call
    /// <see cref="IEventOperations.Append(System.Guid,object[])"/>.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_With_Malformed_NormalizedValue_Returns_400_And_Does_Not_Append()
    {
        // Arrange
        var (controller, session, userId) = BuildController();
        SeedView(session, userId);
        var payload = JsonDocument.Parse("\"not-an-object\"");
        var request = new ReviseAnswerRequestDto(OnboardingTopic.PrimaryGoal, payload);

        // Act
        var result = await controller.ReviseAnswer(request, TestContext.Current.CancellationToken);

        // Assert
        var validationResult = result as ObjectResult;
        validationResult.Should().NotBeNull(because: "malformed payload must produce a validation error result");
        validationResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        session.Events.DidNotReceive().Append(Arg.Any<Guid>(), Arg.Any<object[]>());
    }

    /// <summary>
    /// A payload shaped like <see cref="PrimaryGoalAnswer"/> (goal/description)
    /// sent for a <see cref="OnboardingTopic.TargetEvent"/> topic is missing the
    /// required properties EventName, DistanceKm, and EventDateIso — the endpoint
    /// must return 400 and must not append the event.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_With_Wrong_Topic_Payload_Returns_400_And_Does_Not_Append()
    {
        // Arrange
        var (controller, session, userId) = BuildController();
        SeedView(session, userId);
        var primaryGoalJson = JsonSerializer.Serialize(new { goal = "GeneralFitness", description = "Just keeping fit" });
        var payload = JsonDocument.Parse(primaryGoalJson);
        var request = new ReviseAnswerRequestDto(OnboardingTopic.TargetEvent, payload);

        // Act
        var result = await controller.ReviseAnswer(request, TestContext.Current.CancellationToken);

        // Assert — TargetEventAnswer requires EventName, DistanceKm, EventDateIso which are absent.
        var validationResult = result as ObjectResult;
        validationResult.Should().NotBeNull(because: "wrong-topic payload must produce a validation error result");
        validationResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        session.Events.DidNotReceive().Append(Arg.Any<Guid>(), Arg.Any<object[]>());
    }

    /// <summary>
    /// A fully valid <see cref="PrimaryGoalAnswer"/> payload for the
    /// <see cref="OnboardingTopic.PrimaryGoal"/> topic passes validation and the
    /// action appends the event and returns 200.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_With_Valid_PrimaryGoal_Payload_Appends_And_Returns_200()
    {
        // Arrange
        var (controller, session, userId) = BuildController();
        SeedView(session, userId);
        var validJson = JsonSerializer.Serialize(new { goal = "GeneralFitness", description = "Keeping healthy" });
        var payload = JsonDocument.Parse(validJson);
        var request = new ReviseAnswerRequestDto(OnboardingTopic.PrimaryGoal, payload);

        // Act
        var result = await controller.ReviseAnswer(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull(because: "a valid payload must produce a 200 OK");
        session.Events.Received(1).Append(Arg.Any<Guid>(), Arg.Any<object[]>());
    }

    /// <summary>
    /// The <c>NormalizedValue</c> property on <see cref="ReviseAnswerRequestDto"/>
    /// must carry <c>[JsonRequired]</c> so the ASP.NET Core model binder rejects
    /// a POST body that omits the field with HTTP 400 before the action runs.
    /// This test verifies the attribute is present on the generated property.
    /// </summary>
    [Fact]
    public void ReviseAnswerRequestDto_NormalizedValue_Has_JsonRequired_Attribute()
    {
        // Arrange
        var property = typeof(ReviseAnswerRequestDto)
            .GetProperty(nameof(ReviseAnswerRequestDto.NormalizedValue));

        // Act
        var attribute = property?.GetCustomAttribute<JsonRequiredAttribute>();

        // Assert
        property.Should().NotBeNull(because: "NormalizedValue property must exist on the DTO");
        attribute.Should().NotBeNull(
            because: "[property: JsonRequired] must be applied to NormalizedValue so the ASP.NET Core "
                     + "model binder rejects missing payloads with HTTP 400");
    }

    /// <summary>
    /// The <c>Topic</c> property on <see cref="ReviseAnswerRequestDto"/> must carry
    /// <c>[JsonRequired]</c> so the ASP.NET Core model binder rejects a POST body
    /// that omits the field with HTTP 400.
    /// </summary>
    [Fact]
    public void ReviseAnswerRequestDto_Topic_Has_JsonRequired_Attribute()
    {
        // Arrange
        var property = typeof(ReviseAnswerRequestDto)
            .GetProperty(nameof(ReviseAnswerRequestDto.Topic));

        // Act
        var attribute = property?.GetCustomAttribute<JsonRequiredAttribute>();

        // Assert
        property.Should().NotBeNull(because: "Topic property must exist on the DTO");
        attribute.Should().NotBeNull(
            because: "[property: JsonRequired] must be applied to Topic so the ASP.NET Core "
                     + "model binder rejects missing payloads with HTTP 400");
    }

    /// <summary>
    /// The <c>Text</c> property on <see cref="OnboardingTurnRequestDto"/> must
    /// carry <c>[JsonRequired]</c> so the ASP.NET Core model binder rejects a
    /// POST body that omits the field with HTTP 400.
    /// </summary>
    [Fact]
    public void OnboardingTurnRequestDto_Text_Has_JsonRequired_Attribute()
    {
        // Arrange
        var property = typeof(OnboardingTurnRequestDto)
            .GetProperty(nameof(OnboardingTurnRequestDto.Text));

        // Act
        var attribute = property?.GetCustomAttribute<JsonRequiredAttribute>();

        // Assert
        property.Should().NotBeNull(because: "Text property must exist on the DTO");
        attribute.Should().NotBeNull(
            because: "[property: JsonRequired] must be applied to Text so the ASP.NET Core "
                     + "model binder rejects missing text fields with HTTP 400");
    }

    /// <summary>
    /// The pre-existing <c>IdempotencyKey</c> property on
    /// <see cref="OnboardingTurnRequestDto"/> retains its <c>[JsonRequired]</c>
    /// annotation after the <c>Text</c> annotation was added.
    /// </summary>
    [Fact]
    public void OnboardingTurnRequestDto_IdempotencyKey_Retains_JsonRequired_Attribute()
    {
        // Arrange
        var property = typeof(OnboardingTurnRequestDto)
            .GetProperty(nameof(OnboardingTurnRequestDto.IdempotencyKey));

        // Act
        var attribute = property?.GetCustomAttribute<JsonRequiredAttribute>();

        // Assert
        property.Should().NotBeNull(because: "IdempotencyKey property must exist on the DTO");
        attribute.Should().NotBeNull(
            because: "the pre-existing [property: JsonRequired] on IdempotencyKey must be retained");
    }

    private static (OnboardingController Controller, IDocumentSession Session, Guid UserId) BuildController()
    {
        var bus = Substitute.For<IMessageBus>();
        var session = Substitute.For<IDocumentSession>();
        var logger = NullLogger<OnboardingController>.Instance;

        var controller = new OnboardingController(bus, session, logger);

        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString())],
            authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return (controller, session, UserId);
    }

    private static void SeedView(IDocumentSession session, Guid userId)
    {
        var view = new OnboardingView
        {
            Id = userId,
            UserId = userId,
            Status = OnboardingStatus.InProgress,
        };
        session
            .LoadAsync<OnboardingView>(userId, Arg.Any<CancellationToken>())
            .Returns(view);
    }
}
