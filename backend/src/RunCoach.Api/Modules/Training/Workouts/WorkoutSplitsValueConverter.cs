using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Serializes the typed <c>Splits</c> collection to/from a single <c>jsonb</c>
/// column (DEC-072). Web (camelCase) naming so the stored shape matches the
/// frontend split DTO. EF maps a null collection to a null column without
/// invoking the converter, so an absent <c>Splits</c> stays NULL.
/// </summary>
public sealed class WorkoutSplitsValueConverter : ValueConverter<List<WorkoutSplit>?, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public WorkoutSplitsValueConverter()
        : base(
            splits => JsonSerializer.Serialize(splits, Options),
            json => JsonSerializer.Deserialize<List<WorkoutSplit>>(json, Options))
    {
    }
}
