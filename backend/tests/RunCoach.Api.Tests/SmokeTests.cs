using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RunCoach.Api.Tests;

public class SmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // Integration smoke test: boots the full API via WebApplicationFactory with Testcontainers
    // Postgres. Opt-in only — set RUNCOACH_INTEGRATION_TESTS=1 to include. Not currently
    // wired in CI (the workflow doesn't set the env var), so this test is skipped in CI
    // and in local unit-test iteration runs. Enable locally when validating integration
    // behaviour end-to-end.
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
