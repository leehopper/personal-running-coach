using System;
using Microsoft.Extensions.AI;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Helpers for inserting the <see cref="SanitizationAuditChatClient"/> into
/// an M.E.AI pipeline. The <see cref="UseSanitizationAudit(IChatClient)"/>
/// overload is the no-builder path; once <c>ChatClientBuilder</c> is wired in
/// for Slice 1 (T01.5), the builder overload there will call into this.
/// </summary>
public static class SanitizationAuditChatClientExtensions
{
    /// <summary>
    /// Wraps <paramref name="innerClient"/> in a
    /// <see cref="SanitizationAuditChatClient"/>.
    /// </summary>
    /// <param name="innerClient">The chat client to wrap.</param>
    /// <returns>The wrapped client.</returns>
    public static IChatClient UseSanitizationAudit(this IChatClient innerClient)
    {
        ArgumentNullException.ThrowIfNull(innerClient);
        return new SanitizationAuditChatClient(innerClient);
    }
}
