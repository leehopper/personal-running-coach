using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// EF Core mapping for <see cref="UserSettings"/>: a plain user-keyed row whose
/// <see cref="UserSettings.UserId"/> is both primary key (via <c>[Key]</c>) and
/// the FK to the Identity user. The <see cref="PreferredUnits"/> enum is stored by
/// name so the column survives any future reordering of the enum members.
/// Discovered via <c>ApplyConfigurationsFromAssembly</c>.
/// </summary>
internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        // UserId is the PK and the FK; a deleted user cascades their settings away.
        builder
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(s => s.PreferredUnits)
            .HasConversion(new EnumToStringConverter<PreferredUnits>());
    }
}
