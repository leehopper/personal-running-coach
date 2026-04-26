using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RunCoach.Api.Modules.Identity.Entities;

/// <summary>
/// EF Core fluent configuration for <see cref="UserProfile"/>. Carries the
/// shared-key 1:1 relationship to <see cref="ApplicationUser"/> with cascade
/// delete and the JSONB owned-entity <c>ToJson</c> mappings for the five
/// DEC-047 record-typed slot answers. The categorical <c>PrimaryGoal</c> enum
/// stays as a scalar column — only the typed-record slots round-trip through
/// JSONB. Per spec 13 § Unit 1 R01.3, JSONB lets the answer-record shapes
/// evolve without DDL migrations.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // Shared-key 1:1 with ApplicationUser. UserId is both PK and FK.
        builder
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Five DEC-047 onboarding slot columns mapped as JSONB via owned-entity
        // ToJson. PrimaryGoal is a scalar enum and does not need ToJson.
        builder.OwnsOne(p => p.TargetEvent, b => b.ToJson());
        builder.OwnsOne(p => p.CurrentFitness, b => b.ToJson());
        builder.OwnsOne(p => p.WeeklySchedule, b => b.ToJson());
        builder.OwnsOne(p => p.InjuryHistory, b => b.ToJson());
        builder.OwnsOne(p => p.Preferences, b => b.ToJson());
    }
}
