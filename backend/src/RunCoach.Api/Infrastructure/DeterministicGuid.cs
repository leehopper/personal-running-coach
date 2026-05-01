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

        // RFC 4122 §4.3 specifies SHA-1 as the hash for UUID v5; it is used as
        // an identity-derivation function here, not for any security purpose.
#pragma warning disable CA5350
        SHA1.HashData(input, hash);
#pragma warning restore CA5350

        Span<byte> bytes = stackalloc byte[16];
        hash[..16].CopyTo(bytes);

        // RFC 4122 §4.3: set version (5) and IETF variant bits.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
