using System.Security.Cryptography;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Derives the deterministic <b>server-side</b> turn id for a coach reply from the
/// user turn's client message id (Slice 4B Unit 3, DEC-085). The coach-turn write
/// is idempotent on this derived id: a duplicate completion for the same user turn
/// re-derives the same id and short-circuits via <c>IIdempotencyStore</c>, so the
/// coach turn is never appended twice. The user turn already consumes the client id
/// as its own idempotency key, so the coach turn <b>must</b> use a distinct key —
/// re-using the client id would collide on the idempotency marker insert.
/// </summary>
/// <remarks>
/// A name-based (RFC 4122 §4.3-style) hash of a fixed namespace + the client id,
/// never random, so the derivation is stable across processes and deploys. A
/// re-send after mid-stream death uses a <em>fresh</em> client id (D5), which
/// derives a fresh coach id — the original turn keeps its errored marker and the
/// re-send is a separate turn, by design.
/// </remarks>
public static class ConversationTurnId
{
    // Fixed namespace GUID so the derivation is deterministic and distinct from the
    // user/client GUID space. Arbitrary but stable — never change it, or in-flight
    // idempotency keys would shift.
    private static readonly Guid CoachTurnNamespace = new("7d3f1b2a-9c84-4f6e-b1a2-2c5d6e7f8a90");

    /// <summary>Derives the deterministic coach-turn id for a given user-turn client message id.</summary>
    /// <param name="clientMessageId">The user turn's client-generated message id.</param>
    /// <returns>A stable, well-formed GUID distinct from <paramref name="clientMessageId"/>.</returns>
    public static Guid DeriveCoachTurnId(Guid clientMessageId)
    {
        Span<byte> buffer = stackalloc byte[32];
        _ = CoachTurnNamespace.TryWriteBytes(buffer[..16]);
        _ = clientMessageId.TryWriteBytes(buffer[16..]);

        Span<byte> hash = stackalloc byte[32];
        _ = SHA256.HashData(buffer, hash);

        Span<byte> guidBytes = hash[..16];

        // Stamp RFC 4122 version (8 = custom/hash-based) + variant bits so the value
        // is a well-formed UUID rather than a raw hash slice.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x80);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
