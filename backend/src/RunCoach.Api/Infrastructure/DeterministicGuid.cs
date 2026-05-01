using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Helpers for deriving deterministic Guids from a stable input pair (typically
/// a user id plus a stream-purpose tag). Produces a UUID-v5-shaped Guid: SHA-1
/// over the UTF-8 bytes of <c>{userId:D}:{streamPurpose}</c>, truncated to
/// 16 bytes with the version (5) and IETF variant bits set.
/// </summary>
/// <remarks>
/// <para>
/// Use this for Marten stream ids on aggregates that are 1:1 with the user.
/// Repeating the call returns the same Guid, so <c>StartStream&lt;T&gt;</c>
/// retries hit a primary-key violation and surface as "already started"
/// rather than creating a duplicate stream. See backend/REVIEW.md "Marten
/// event-stream identity".
/// </para>
/// </remarks>
public static class DeterministicGuid
{
    /// <summary>
    /// Derives a UUID-v5-shaped Guid from a user id plus a stream-purpose
    /// label. Same inputs always yield the same Guid.
    /// </summary>
    /// <param name="userId">The runner's authenticated user id.</param>
    /// <param name="streamPurpose">
    /// Short stable label that disambiguates this stream from other 1:1 streams
    /// the same user owns (e.g. <c>"onboarding"</c>). Trimmed and case-sensitive.
    /// </param>
    /// <returns>A deterministic Guid suitable for use as a Marten stream id.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="streamPurpose"/> is null, empty, or whitespace.
    /// </exception>
    [SuppressMessage(
        "Security",
        "S4790:Using weak hashing algorithms is security-sensitive",
        Justification = "RFC 4122 §4.3 mandates SHA-1 for UUID v5; SHA-1 is used here as an identity-derivation function (deterministic id from a name+namespace pair), not for any security primitive (no signing, password hashing, MAC, or integrity check). The output is a 16-byte stream id, never a credential.")]
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 4122 §4.3 specifies SHA-1 for UUID v5; non-cryptographic identity derivation only.")]
    public static Guid From(Guid userId, string streamPurpose)
    {
        if (string.IsNullOrWhiteSpace(streamPurpose))
        {
            throw new ArgumentException("Stream purpose must be non-empty.", nameof(streamPurpose));
        }

        var input = Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{userId:D}:{streamPurpose}"));

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(input, hash);

        Span<byte> bytes = stackalloc byte[16];
        hash[..16].CopyTo(bytes);

        // RFC 4122 §4.3: set version (5) and IETF variant bits in the canonical
        // big-endian layout (byte 6 holds the version nibble, byte 8 holds the
        // variant bits).
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // Construct from big-endian bytes so the canonical layout is preserved
        // (Guid's default ctor expects Windows mixed-endian; using the
        // bigEndian:true overload introduced in .NET 8 keeps the version digit
        // visible in `ToString("D")` and `ToByteArray(bigEndian: true)`).
        return new Guid(bytes, bigEndian: true);
    }
}
