using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RunCoach.Api.Tests;

public class SmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // Integration smoke test: boots the full API via WebApplicationFactory with Testcontainers
    // Postgres. Opt-in only — set RUNCOACH_INTEGRATION_TESTS=1 to include (CI does this).
    // Local dev runs skip by default so unit-test iteration stays fast.
    private static bool IntegrationTestsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUNCOACH_INTEGRATION_TESTS"),
            "1",
            StringComparison.Ordinal);

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        if (!IntegrationTestsEnabled)
        {
            Assert.Skip(
                "Integration tests are opt-in. Set RUNCOACH_INTEGRATION_TESTS=1 to run SmokeTests against a Dockerized Postgres.");
        }

        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
