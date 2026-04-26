using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Modules.Identity.Entities;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Primary EF Core DbContext for RunCoach. Owns the ASP.NET Core Identity schema
/// (users, roles, claims, logins, tokens) and the Data Protection keys table.
/// Wolverine envelope storage is injected at runtime by
/// <c>AddDbContextWithWolverineIntegration</c>'s <c>WolverineModelCustomizer</c>
/// so the outbox, Identity tables, and future relational user-state all commit
/// inside a single Postgres transaction. Migrations skip the envelope tables
/// (they're <c>ExcludeFromMigrations</c>); Wolverine provisions them itself via
/// <c>ApplyAllDatabaseChangesOnStartup</c>.
/// </summary>
public class RunCoachDbContext(DbContextOptions<RunCoachDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Pin every EF entity to the `public` schema explicitly. The
        // `Marten.EntityFrameworkCore` projection registration in
        // <c>MartenConfiguration</c> introspects this DbContext via
        // <c>AddEntityTablesFromDbContext</c>; pinning the schema guards
        // against any future Marten-side path that inspects <c>GetSchema()</c>
        // on the entity types and would otherwise default to Marten's
        // <c>runcoach_events</c> schema.
        builder.HasDefaultSchema("public");

        // Discover IEntityTypeConfiguration<T> implementations co-located with
        // their entities (e.g. UserProfileConfiguration). Keeps DbContext free
        // of per-entity fluent wiring.
        builder.ApplyConfigurationsFromAssembly(typeof(RunCoachDbContext).Assembly);
    }
}
