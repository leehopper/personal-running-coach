using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Structural change-tracking comparer for the value-converted <c>Splits</c>
/// collection. <see cref="WorkoutSplit"/> is a record (value equality), so
/// sequence equality is correct; the snapshot copies the list so EF detects
/// in-place mutation. The comparer is typed for the nullable
/// <see cref="WorkoutLog.Splits"/> property, so every delegate — equality, hash,
/// and snapshot — handles a null collection rather than relying on EF to never
/// route a null through them.
/// </summary>
public sealed class WorkoutSplitsValueComparer : ValueComparer<List<WorkoutSplit>?>
{
    public WorkoutSplitsValueComparer()
        : base(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v == null ? 0 : v.Aggregate(0, (hash, split) => HashCode.Combine(hash, split.GetHashCode())),
            v => v == null ? null : v.ToList())
    {
    }
}
