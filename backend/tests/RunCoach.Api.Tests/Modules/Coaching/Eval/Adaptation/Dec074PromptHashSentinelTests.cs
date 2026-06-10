using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Deterministic tests for the DEC-074 prompt-hash sentinel (Slice 3 Unit 6):
/// the committed <c>Prompts/.prompt-hashes.sha256</c> manifest records every
/// coaching prompt's SHA-256, the C# manifest computation stays byte-identical to
/// the committed (script-generated) file in the canonical format, and an
/// un-re-recorded prompt edit changes the recorded hash — the exact drift the
/// <c>EvalTestBase</c> static-ctor backstop and the <c>check-prompt-hashes</c>
/// lefthook hook fail on.
/// </summary>
[Trait("Category", "Eval")]
public sealed partial class Dec074PromptHashSentinelTests
{
    private const string AdaptationPromptFileName = "adaptation.v1.yaml";

    [Fact]
    public void Manifest_RecordsTheAdaptationPromptHash()
    {
        // Arrange
        var promptsDir = EvalTestBase.GetPromptsDirectory();
        var expectedHash = Sha256Lower(Path.Combine(promptsDir, AdaptationPromptFileName));
        var manifest = ReadManifest(promptsDir);

        // Assert — the manifest line for the adaptation prompt carries its current hash.
        manifest.Should().Contain($"{expectedHash}  {AdaptationPromptFileName}");
    }

    [Fact]
    public void Manifest_IsInSyncWithThePromptFiles()
    {
        // Arrange — the C# computation must equal the committed (script-generated) manifest.
        var promptsDir = EvalTestBase.GetPromptsDirectory();
        var computed = EvalTestBase.ComputePromptHashManifest().Replace("\r\n", "\n").TrimEnd('\n');
        var committed = ReadManifest(promptsDir).Replace("\r\n", "\n").TrimEnd('\n');

        // Assert
        computed.Should().Be(
            committed,
            because: "the C# backstop and the committed manifest (written by check-prompt-hashes.sh) must agree byte-for-byte");
    }

    [Fact]
    public void Manifest_UsesTheCanonicalFormat()
    {
        // Arrange
        var committed = ReadManifest(EvalTestBase.GetPromptsDirectory()).Replace("\r\n", "\n");
        var lines = committed.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert — every line is "<64 lowercase hex><two spaces><bare *.yaml filename>".
        lines.Should().NotBeEmpty();
        lines.Should().AllSatisfy(line =>
            ManifestLinePattern().IsMatch(line).Should().BeTrue(because: $"line '{line}' must use the sha256sum layout"));
    }

    [Fact]
    public void PromptEdit_ChangesTheRecordedHash_SoTheSentinelWouldFire()
    {
        // Arrange — the committed hash for the adaptation prompt, and a one-byte edit of its content.
        var promptsDir = EvalTestBase.GetPromptsDirectory();
        var promptBytes = File.ReadAllBytes(Path.Combine(promptsDir, AdaptationPromptFileName));
        var committedHash = Convert.ToHexStringLower(SHA256.HashData(promptBytes));

        var editedBytes = promptBytes.Concat("\n# stray edit\n"u8.ToArray()).ToArray();
        var editedHash = Convert.ToHexStringLower(SHA256.HashData(editedBytes));

        // Assert — an edit yields a different hash that no longer matches the committed
        // manifest line, so ComputePromptHashManifest would diverge and the backstop throws.
        editedHash.Should().NotBe(committedHash);
        ReadManifest(promptsDir).Should().Contain($"{committedHash}  {AdaptationPromptFileName}");
        ReadManifest(promptsDir).Should().NotContain($"{editedHash}  {AdaptationPromptFileName}");
    }

    [Fact]
    public void Backstop_Throws_OnASingleLineOfDrift()
    {
        // Arrange — one hash line differs between computed and committed.
        var committed = $"{new string('a', 64)}  adaptation.v1.yaml\n{new string('b', 64)}  coaching-system.v1.yaml\n";
        var computed = $"{new string('a', 64)}  adaptation.v1.yaml\n{new string('c', 64)}  coaching-system.v1.yaml\n";

        // Act
        var act = () => EvalTestBase.VerifyManifest(computed, committed, "manifest-path");

        // Assert — the drift branch fires (the silent-pass DEC-074 exists to prevent).
        act.Should().Throw<InvalidOperationException>().WithMessage("*not*re-recorded*");
    }

    [Fact]
    public void Backstop_DoesNotThrow_OnLineEndingOrTrailingNewlineOnlyDifferences()
    {
        // Arrange — same hashes, but CRLF line endings and an extra trailing newline.
        var computed = $"{new string('a', 64)}  adaptation.v1.yaml\n{new string('b', 64)}  coaching-system.v1.yaml\n";
        var committed = computed.Replace("\n", "\r\n") + "\r\n";

        // Act
        var act = () => EvalTestBase.VerifyManifest(computed, committed, "manifest-path");

        // Assert — normalization swallows platform noise without masking real drift.
        act.Should().NotThrow();
    }

    [Fact]
    public void Backstop_Throws_WhenTheCommittedManifestIsMissing()
    {
        // Act — a null committed manifest models the missing-file branch.
        var act = () => EvalTestBase.VerifyManifest(
            $"{new string('a', 64)}  adaptation.v1.yaml\n", committedManifest: null, manifestPath: "manifest-path");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*manifest missing*manifest-path*");
    }

    private static string ReadManifest(string promptsDir) =>
        File.ReadAllText(Path.Combine(promptsDir, ".prompt-hashes.sha256"));

    private static string Sha256Lower(string filePath) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(filePath)));

    [GeneratedRegex(@"^[0-9a-f]{64}  [^\s]+\.yaml$")]
    private static partial Regex ManifestLinePattern();
}
