using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Entities;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// Per-user application settings (Slice 4C-units / DEC-086). A plain, mutable,
/// user-keyed EF row — last-write-wins, no event history — decoupled from the
/// onboarding event stream so units are editable post-onboarding without
/// mutating onboarding answers.
/// </summary>
/// <remarks>
/// Deliberately NOT <c>ITenanted</c>: the row is written and read only by the
/// authenticated <see cref="SettingsController"/> keyed on the caller's own
/// <see cref="UserId"/>, never by a Marten projection or a Wolverine handler. The
/// implicit conjoined-tenancy query filter that <c>ITenanted</c> would attach buys
/// nothing here and only risks silently hiding a row whose TenantId did not match
/// the ambient tenant from an authenticated read.
/// </remarks>
[Table("UserSettings")]
public class UserSettings
{
    /// <summary>Gets or sets the owning runner's user id — the primary key (one settings row per user).</summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the navigation to the owning Identity user.</summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Gets or sets the runner's preferred distance units. Frontend-display-only
    /// (DEC-086 D3): stored data, the wire, and the plan-gen prompt stay canonical
    /// km/SI — this preference drives display conversion on the client only, never
    /// any server-side or LLM unit math (DEC-010 / DEC-041). Defaults to
    /// <see cref="PreferredUnits.Kilometers"/>.
    /// </summary>
    public PreferredUnits PreferredUnits { get; set; } = PreferredUnits.Kilometers;

    /// <summary>Gets or sets the audit timestamp for when the settings row was created.</summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>Gets or sets the audit timestamp for when the settings row was last modified.</summary>
    public DateTimeOffset ModifiedOn { get; set; }
}
