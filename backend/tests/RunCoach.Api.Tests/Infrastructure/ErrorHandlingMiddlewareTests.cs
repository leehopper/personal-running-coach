using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Tests.Infrastructure;

public class ErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task TryHandleAsync_SetsStatus500_AndDelegatesToProblemDetailsService()
    {
        // Arrange
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var handler = new ErrorHandlingMiddleware(
            problemDetailsService,
            environment,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/explode";
        var exception = new InvalidOperationException("boom");

        // Act
        var actualHandled = await handler.TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken);

        // Assert
        actualHandled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        await problemDetailsService.Received(1).TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_EmitsVerboseDetailContainingExceptionToString()
    {
        // Arrange
        ProblemDetailsContext? capturedContext = null;
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(ctx => capturedContext = ctx))
            .Returns(true);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var handler = new ErrorHandlingMiddleware(
            problemDetailsService,
            environment,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        var httpContext = new DefaultHttpContext { Request = { Path = "/explode" } };
        var exception = new InvalidOperationException("dev-detail-marker");

        // Act
        await handler.TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Exception.Should().BeSameAs(exception);
        var problemDetails = capturedContext.ProblemDetails;
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Detail.Should().Contain("dev-detail-marker");
        problemDetails.Detail.Should().Contain("InvalidOperationException");
        problemDetails.Instance.Should().Be("/explode");
    }

    [Fact]
    public async Task TryHandleAsync_OutsideDevelopment_EmitsGenericDetailWithoutStackTrace()
    {
        // Arrange
        ProblemDetailsContext? capturedContext = null;
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(ctx => capturedContext = ctx))
            .Returns(true);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var handler = new ErrorHandlingMiddleware(
            problemDetailsService,
            environment,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        var httpContext = new DefaultHttpContext { Request = { Path = "/explode" } };
        var exception = new InvalidOperationException("prod-detail-marker");

        // Act
        await handler.TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken);

        // Assert
        capturedContext.Should().NotBeNull();
        var problemDetails = capturedContext!.ProblemDetails;
        problemDetails.Detail.Should().NotContain("prod-detail-marker");
        problemDetails.Detail.Should().NotContain("InvalidOperationException");
        problemDetails.Detail.Should().Be("An unexpected error occurred while processing the request.");
    }

    [Fact]
    public async Task TryHandleAsync_PropagatesFalseWhenProblemDetailsServiceCannotWrite()
    {
        // Arrange
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(false);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var handler = new ErrorHandlingMiddleware(
            problemDetailsService,
            environment,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        // Act
        var actualHandled = await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        // Assert
        actualHandled.Should().BeFalse();
    }
}
