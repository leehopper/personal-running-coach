using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Prompts;

namespace RunCoach.Api.Tests.Modules.Coaching.Prompts;

public sealed class YamlPromptStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger<YamlPromptStore> _logger = Substitute.For<ILogger<YamlPromptStore>>();

    public YamlPromptStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-store-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetPromptAsync_ValidYaml_ReturnsTemplate()
    {
        // Arrange
        var yaml = BuildFullYaml();
        WriteYamlFile("coaching-system.v1.yaml", yaml);
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var actual = await sut.GetPromptAsync("coaching-system", "v1", TestContext.Current.CancellationToken);

        // Assert
        actual.Id.Should().Be("coaching-system");
        actual.Version.Should().Be("v1");
        actual.StaticSystemPrompt.Should().Contain("running coach");
        actual.ContextTemplate.Should().Contain("{{profile}}");
        actual.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPromptAsync_ValidYaml_DeserializesMetadata()
    {
        // Arrange
        var yaml = BuildFullYaml();
        WriteYamlFile("coaching-system.v1.yaml", yaml);
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var actual = await sut.GetPromptAsync("coaching-system", "v1", TestContext.Current.CancellationToken);
        var metadata = actual.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata!.Description.Should().Be("Test prompt");
        metadata!.Author.Should().Be("Test");
    }

    [Fact]
    public async Task GetPromptAsync_MissingFile_Throws()
    {
        // Arrange
        var sut = CreateStore("nonexistent", "v1");

        // Act
        var act = () => sut.GetPromptAsync("nonexistent", "v1", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetPromptAsync_SecondCall_ReturnsCached()
    {
        // Arrange
        var yaml = BuildMinimalYaml("You are a coach.");
        WriteYamlFile("coaching-system.v1.yaml", yaml);
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var first = await sut.GetPromptAsync("coaching-system", "v1", TestContext.Current.CancellationToken);
        var second = await sut.GetPromptAsync("coaching-system", "v1", TestContext.Current.CancellationToken);

        // Assert
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetPromptAsync_NoMetadata_ReturnsNull()
    {
        // Arrange
        var yaml = BuildMinimalYaml("Minimal prompt.");
        WriteYamlFile("minimal.v1.yaml", yaml);
        var sut = CreateStore("minimal", "v1");

        // Act
        var actual = await sut.GetPromptAsync("minimal", "v1", TestContext.Current.CancellationToken);

        // Assert
        actual.Metadata.Should().BeNull();
        actual.StaticSystemPrompt.Should().Contain("Minimal prompt");
    }

    [Fact]
    public void GetActiveVersion_Configured_ReturnsVersion()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var actual = sut.GetActiveVersion("coaching-system");

        // Assert
        actual.Should().Be("v1");
    }

    [Fact]
    public void GetActiveVersion_Unconfigured_Throws()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var act = () => sut.GetActiveVersion("unknown-prompt");

        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ValidateConfiguredVersions_FilesExist_NoThrow()
    {
        // Arrange
        var yaml = BuildMinimalYaml("Coach prompt.");
        WriteYamlFile("coaching-system.v1.yaml", yaml);
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var act = () => sut.ValidateConfiguredVersions();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateConfiguredVersions_MissingFile_Throws()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v2");

        // Act
        var act = () => sut.ValidateConfiguredVersions();

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task GetPromptAsync_EmptyId_Throws()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var act = () => sut.GetPromptAsync(string.Empty, "v1", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPromptAsync_PreservesNewlines()
    {
        // Arrange
        var yaml = BuildMultilineYaml();
        WriteYamlFile("multiline.v1.yaml", yaml);
        var sut = CreateStore("multiline", "v1");

        // Act
        var actual = await sut.GetPromptAsync("multiline", "v1", TestContext.Current.CancellationToken);

        // Assert
        actual.StaticSystemPrompt.Should().Contain("Line one.");
        actual.StaticSystemPrompt.Should().Contain("Line two.");
    }

    [Fact]
    public async Task GetPromptAsync_IdContainsDoubleColon_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var act = () => sut.GetPromptAsync("bad::id", "v1", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Prompt id must not contain*");
    }

    [Fact]
    public async Task GetPromptAsync_VersionContainsDoubleColon_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateStore("coaching-system", "v1");

        // Act
        var act = () => sut.GetPromptAsync("coaching-system", "v::1", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Version must not contain*");
    }

    [Fact]
    public async Task GetPromptAsync_FailThenSucceed_EvictsCacheAndRetries()
    {
        // Arrange — no file on disk yet
        var sut = CreateStore("late-prompt", "v1");

        // Act — first call fails (file missing)
        var firstAct = () => sut.GetPromptAsync("late-prompt", "v1", TestContext.Current.CancellationToken);
        await firstAct.Should().ThrowAsync<KeyNotFoundException>();

        // Arrange — now create the file so the retry can succeed
        WriteYamlFile("late-prompt.v1.yaml", BuildMinimalYaml("Now I exist."));

        // Act — second call should succeed because the faulted entry was evicted
        var actual = await sut.GetPromptAsync("late-prompt", "v1", TestContext.Current.CancellationToken);

        // Assert
        actual.Id.Should().Be("late-prompt");
        actual.Version.Should().Be("v1");
        actual.StaticSystemPrompt.Should().Contain("Now I exist.");
    }

    [Fact]
    public async Task GetPromptAsync_ConcurrentCalls_ReturnSameInstance()
    {
        // Arrange
        var yaml = BuildMinimalYaml("You are a coach.");
        WriteYamlFile("coaching-system.v1.yaml", yaml);
        var sut = CreateStore("coaching-system", "v1");

        // Act — fire multiple calls concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.GetPromptAsync("coaching-system", "v1", TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert — all results are the same reference (loaded once, cached)
        var expected = results[0];
        foreach (var actual in results)
        {
            actual.Should().BeSameAs(expected);
        }
    }

    private static string BuildFullYaml()
    {
        return string.Join(
            "\n",
            "metadata:",
            "  description: \"Test prompt\"",
            "  author: \"Test\"",
            "  created_at: \"2026-01-01\"",
            "static_system_prompt: |",
            "  You are a running coach.",
            "context_template: |",
            "  === PROFILE ===",
            "  {{profile}}",
            string.Empty);
    }

    private static string BuildMinimalYaml(string prompt)
    {
        return string.Join(
            "\n",
            "static_system_prompt: |",
            $"  {prompt}",
            "context_template: |",
            "  {{profile}}",
            string.Empty);
    }

    private static string BuildMultilineYaml()
    {
        return string.Join(
            "\n",
            "static_system_prompt: |",
            "  Line one.",
            "  Line two.",
            "  Line three.",
            "context_template: |",
            "  {{profile}}",
            string.Empty);
    }

    private YamlPromptStore CreateStore(string promptId, string activeVersion)
    {
        var settings = new PromptStoreSettings
        {
            ActiveVersions = new Dictionary<string, string>
            {
                [promptId] = activeVersion,
            },
        };

        return new YamlPromptStore(settings, _tempDir, _logger);
    }

    private void WriteYamlFile(string fileName, string content)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
    }
}
