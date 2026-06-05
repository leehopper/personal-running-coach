using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// EF Core mapping for <see cref="WorkoutLog"/>: value-converted distance/duration
/// (the repo's first converters), an open <c>jsonb</c> metrics bag, a typed
/// <c>jsonb</c> splits column, and the nullable EF Core 10 optional complex-type
/// prescription snapshot (table-split to real columns). Discovered via
/// <c>ApplyConfigurationsFromAssembly</c>.
/// </summary>
internal sealed class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> builder)
    {
        // UserId FK to the Identity user; a deleted user cascades their logs away.
        builder
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(w => w.Distance).HasConversion(new DistanceValueConverter());
        builder.Property(w => w.Duration).HasConversion(new DurationValueConverter());

        // Open optional-metrics bag — raw JSON owned by the API (DEC-072).
        builder.Property(w => w.Metrics).HasColumnType("jsonb");

        // Typed splits serialized into a single jsonb column.
        builder
            .Property(w => w.Splits)
            .HasConversion(new WorkoutSplitsValueConverter(), new WorkoutSplitsValueComparer())
            .HasColumnType("jsonb");

        // Server-authoritative prescription snapshot (DEC-076): EF Core 10 optional
        // complex type, table-split. Distance/Duration/Pace bounds value-converted
        // on their complex-type member properties; the computed PaceRange view is
        // ignored (a single converter cannot span the two pace columns).
        builder.ComplexProperty(w => w.Prescription, prescription =>
        {
            // Store WorkoutType by name, not ordinal: the snapshot is durable state,
            // and the name survives reordering/insertion of enum members (which the
            // int encoding would not). Matches WorkoutType's JSON representation.
            prescription.Property(p => p.WorkoutType).HasConversion(new EnumToStringConverter<WorkoutType>());
            prescription.Property(p => p.PrescribedDistance).HasConversion(new DistanceValueConverter());
            prescription.Property(p => p.PrescribedDuration).HasConversion(new DurationValueConverter());
            prescription.Property(p => p.PrescribedPaceFast).HasConversion(new PaceValueConverter());
            prescription.Property(p => p.PrescribedPaceSlow).HasConversion(new PaceValueConverter());
            prescription.Ignore(p => p.PrescribedPace);
        });

        // Supports the newest-first keyset read-by-user.
        builder.HasIndex(w => new { w.UserId, w.OccurredOn, w.WorkoutLogId });
    }
}
