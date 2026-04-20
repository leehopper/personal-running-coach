using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Modules.Identity.Entities;
using Wolverine.EntityFrameworkCore;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Primary EF Core DbContext for RunCoach. Owns the ASP.NET Core Identity schema
/// (users, roles, claims, logins, tokens), the Data Protection keys table, and
/// Wolverine envelope storage so outbox messages, Identity tables, and future
/// relational user-state all commit inside a single Postgres transaction.
/// </summary>
public class RunCoachDbContext(DbContextOptions<RunCoachDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Register Wolverine envelope storage tables in the same migration stream
        // so outbox + Identity + DataProtection keys share one Postgres transaction.
        builder.MapWolverineEnvelopeStorage();
    }
}
