using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NSubstitute;
using RunCoach.Api.Modules.Identity;

namespace RunCoach.Api.Tests.Modules.Identity;

/// <summary>
/// Unit coverage for <see cref="IdentityResultExtensions.ToRegistrationActionResult"/>
/// across every branch: guard against successful result, uniqueness-conflict
/// short circuit (409 plain ProblemDetails), per-field validation (400
/// ValidationProblemDetails keyed by DTO property), and the non-field
/// <c>general</c>-bucket path.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IdentityResultExtensionsTests
{
    [Fact]
    public void ToRegistrationActionResult_Throws_WhenResultIsSuccess()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();

        // Act
        var act = () => IdentityResult.Success.ToRegistrationActionResult(controller);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ToRegistrationActionResult called on a successful IdentityResult*");
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_409_ForDuplicateEmailConflict()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.DuplicateEmail),
            Description = "Email already taken.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Type.Should().Be("https://runcoach.app/problems/registration-conflict");
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_409_ForDuplicateUserNameConflict()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.DuplicateUserName),
            Description = "Username already taken.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_400_WithPasswordBucket_ForWeakPassword()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.PasswordTooShort),
            Description = "Password too short.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        controller.ModelState.Should().ContainKey("password");
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_400_WithEmailBucket_ForInvalidEmail()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.InvalidEmail),
            Description = "Email is not well-formed.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        controller.ModelState.Should().ContainKey("email");
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_400_WithEmailBucket_ForInvalidUserName()
    {
        // Arrange — Register sets UserName = Email, so UserName errors must
        // surface on the Email DTO property (there is no UserName field on
        // the wire).
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.InvalidUserName),
            Description = "Username contains disallowed characters.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        controller.ModelState.Should().ContainKey("email");
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_400_WithGeneralBucket_ForDefaultError()
    {
        // Arrange — DefaultError is the catch-all Identity emits when no
        // specific code applies; it must surface under the canonical
        // `general` bucket rather than the empty-string key the default
        // switch arm used to produce (DEC-052).
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = nameof(IdentityErrorDescriber.DefaultError),
            Description = "An unknown failure has occurred.",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        controller.ModelState.Should().ContainKey(IdentityErrorBuckets.General);
        controller.ModelState.Should().NotContainKey(string.Empty);
    }

    [Fact]
    public void ToRegistrationActionResult_Returns_400_WithGeneralBucket_ForUnknownCode()
    {
        // Arrange — same contract for any code the mapper doesn't recognize
        // (including a future Identity SDK rename that silently drops an
        // existing code).
        var controller = CreateControllerWithProblemDetailsFactory();
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "SomethingNewIdentityWillAddLater",
            Description = "unexpected",
        });

        // Act
        var actual = result.ToRegistrationActionResult(controller);

        // Assert
        var objectResult = actual.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        controller.ModelState.Should().ContainKey(IdentityErrorBuckets.General);
    }

    [Fact]
    public void ToRegistrationActionResult_ThrowsArgumentNullException_ForNullResult()
    {
        // Arrange
        var controller = CreateControllerWithProblemDetailsFactory();

        // Act
        var act = () => IdentityResultExtensions.ToRegistrationActionResult(null!, controller);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void ToRegistrationActionResult_ThrowsArgumentNullException_ForNullController()
    {
        // Arrange + Act
        var act = () => IdentityResult.Failed().ToRegistrationActionResult(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("controller");
    }

    private static TestController CreateControllerWithProblemDetailsFactory()
    {
        // `ControllerBase.Problem` and `ControllerBase.ValidationProblem`
        // delegate to the factory; NSubstitute returns a bare ProblemDetails
        // / ValidationProblemDetails instance whose `Status` / `Type` survive
        // the ObjectResult wrapper, which is all the assertions above inspect.
        var factory = Substitute.For<ProblemDetailsFactory>();
        factory.CreateProblemDetails(
            Arg.Any<HttpContext>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>())
            .Returns(call => new ProblemDetails
            {
                Status = call.ArgAt<int?>(1),
                Title = call.ArgAt<string?>(2),
                Type = call.ArgAt<string?>(3),
                Detail = call.ArgAt<string?>(4),
                Instance = call.ArgAt<string?>(5),
            });
        factory.CreateValidationProblemDetails(
            Arg.Any<HttpContext>(),
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>())
            .Returns(call => new ValidationProblemDetails(
                call.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(1))
            {
                Status = call.ArgAt<int?>(2) ?? StatusCodes.Status400BadRequest,
            });

        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
            ProblemDetailsFactory = factory,
        };
        return controller;
    }

    private sealed class TestController : ControllerBase;
}
