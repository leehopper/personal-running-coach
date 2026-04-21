using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// DbContext dedicated to ASP.NET Core Data Protection key persistence. Schema
/// for the <c>DataProtectionKeys</c> table is created by <see cref="RunCoachDbContext"/>'s
/// initial migration so a single EF migration stream owns all slice-0 tables; this
/// context exposes the entity set that <see cref="IDataProtectionKeyContext"/> consumers need.
/// </summary>
public class DpKeysContext(DbContextOptions<DpKeysContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
