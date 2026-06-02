using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Structural change-tracking comparer for the value-converted <c>Splits</c>
/// collection. <see cref="WorkoutSplit"/> is a record (value equality), so
/// sequence equality is correct; the snapshot copies the list so EF detects
/// in-place mutation.
/// </summary>
public sealed class WorkoutSplitsValueComparer : ValueComparer<List<WorkoutSplit>>
{
    public WorkoutSplitsValueComparer()
        : base(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (hash, split) => HashCode.Combine(hash, split.GetHashCode())),
            v => v.ToList())
    {
    }
}
