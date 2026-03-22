using Anthropic;
using Anthropic.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Configuration;
using XunitAssert = Xunit.Assert;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Spike test to verify that the Anthropic IChatClient bridge composes
/// correctly with M.E.AI.Evaluation.Reporting caching decorator.
///
/// This is the T03.1 gate — if this fails, the fallback is a custom
/// caching DelegatingHandler (changing the T03.2+ implementation approach).
/// </summary>
[Trait("Category", "Eval")]
public sealed class IChatClientCachingSpikeTests : IAsyncDisposable
{
    private readonly string? _apiKey;
    private readonly string _storagePath;

    public IChatClientCachingSpikeTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<IChatClientCachingSpikeTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        _apiKey = configuration["Anthropic:ApiKey"];

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        }

        _storagePath = Path.Combine(
            Path.GetTempPath(),
            "runcoach-eval-cache-spike",
            Guid.NewGuid().ToString("N")[..8]);
    }

    [Fact]
    public async Task Anthropic_IChatClient_composes_with_MEAI_caching_decorator()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            XunitAssert.Fail("Anthropic API key not configured — set via user-secrets.");
            return;
        }

        // Arrange — create an Anthropic IChatClient via the official SDK bridge
        var anthropicClient = new AnthropicClient(new ClientOptions { ApiKey = _apiKey });
        IChatClient innerClient = anthropicClient.AsIChatClient("claude-haiku-4-5-20251001", 256);

        var chatConfig = new ChatConfiguration(innerClient);

        var reportingConfig = DiskBasedReportingConfiguration.Create(
            storageRootPath: _storagePath,
            evaluators: [],
            chatConfiguration: chatConfig,
            enableResponseCaching: true,
            executionName: "spike-test");

        const string scenarioName = "caching-spike";

        // Act — first call (should hit the API)
        ChatResponse firstResponse;
        await using (var run1 = await reportingConfig.CreateScenarioRunAsync(scenarioName))
        {
            var cachedClient = run1.ChatConfiguration!.ChatClient;

            IList<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, "You are a helpful assistant. Reply in exactly one sentence."),
                new ChatMessage(ChatRole.User, "What is the capital of France?"),
            ];

            var options = new ChatOptions { Temperature = 0.0f };
            firstResponse = await cachedClient.GetResponseAsync(messages, options);
        }

        firstResponse.Text.Should().NotBeNullOrWhiteSpace("first call should return a response from the API");

        // Act — second call with identical parameters (should serve from cache)
        ChatResponse secondResponse;
        await using (var run2 = await reportingConfig.CreateScenarioRunAsync(scenarioName))
        {
            var cachedClient = run2.ChatConfiguration!.ChatClient;

            IList<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, "You are a helpful assistant. Reply in exactly one sentence."),
                new ChatMessage(ChatRole.User, "What is the capital of France?"),
            ];

            var options = new ChatOptions { Temperature = 0.0f };
            secondResponse = await cachedClient.GetResponseAsync(messages, options);
        }

        // Assert — both responses should be identical (second served from cache)
        secondResponse.Text.Should().NotBeNullOrWhiteSpace("second call should return a response from cache");
        secondResponse.Text.Should().Be(
            firstResponse.Text,
            "cached response should be identical to the original API response");

        // Verify cache files were created on disk
        Directory.Exists(_storagePath).Should().BeTrue("cache storage directory should exist");
        Directory.GetFiles(_storagePath, "*", SearchOption.AllDirectories)
            .Should().NotBeEmpty("cache should contain persisted response files");
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up the temp cache directory
        if (Directory.Exists(_storagePath))
        {
            await Task.Run(() =>
            {
                try
                {
                    Directory.Delete(_storagePath, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup
                }
            });
        }
    }
}
