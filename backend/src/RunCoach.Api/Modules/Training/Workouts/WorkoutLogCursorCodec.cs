using System.Globalization;
using System.Text;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Encodes/decodes the keyset <see cref="WorkoutLogCursor"/> as an opaque base64
/// token for the history-query wire contract (slice-2b Unit 4). The payload is the
/// last page tail's <c>OccurredOn</c> (ISO <c>yyyy-MM-dd</c>) and
/// <c>WorkoutLogId</c>, both load-bearing for the <c>OccurredOn DESC,
/// WorkoutLogId DESC</c> keyset (the id breaks ties within a date), so a token
/// that drops or corrupts either is rejected rather than silently mis-paging. The
/// token is opaque: clients round-trip it verbatim and never parse it.
/// </summary>
public static class WorkoutLogCursorCodec
{
    private const char Separator = '|';
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Encodes a cursor as an opaque base64 token.</summary>
    public static string Encode(WorkoutLogCursor cursor)
    {
        var payload = string.Concat(
            cursor.OccurredOn.ToString(DateFormat, CultureInfo.InvariantCulture),
            Separator,
            cursor.WorkoutLogId.ToString("D", CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Attempts to decode an opaque token back into a <see cref="WorkoutLogCursor"/>.
    /// Returns <see langword="false"/> for any malformed token — bad base64, the
    /// wrong field shape, or an unparseable date/guid — so the caller can reject it
    /// as a 400 rather than page from a corrupted anchor.
    /// </summary>
    public static bool TryDecode(string? token, out WorkoutLogCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(bytes);
        var separatorIndex = payload.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == payload.Length - 1)
        {
            return false;
        }

        var datePart = payload[..separatorIndex];
        var idPart = payload[(separatorIndex + 1)..];
        if (!DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredOn)
            || !Guid.TryParseExact(idPart, "D", out var workoutLogId))
        {
            return false;
        }

        cursor = new WorkoutLogCursor(occurredOn, workoutLogId);
        return true;
    }
}
