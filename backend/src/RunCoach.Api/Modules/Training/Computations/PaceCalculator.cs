using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Derives training pace zones from VDOT values using lookup tables
/// based on Daniels' Running Formula. Interpolates between table entries
/// for non-integer VDOT values.
/// </summary>
public sealed class PaceCalculator : IPaceCalculator
{
    /// <summary>
    /// Daniels' pace table entries keyed by integer VDOT.
    /// Each entry contains paces per kilometer in seconds:
    /// (EasyMin, EasyMax, Marathon, Threshold, Interval, Repetition).
    /// Values from published Daniels' Running Formula (4th edition) pace tables.
    ///
    /// VDOT 50 values verified against the published 4th edition per-1000m columns
    /// and Daniels-Gilbert equation cross-reference (DEC-040, R-019). VDOT 51-85
    /// corrected from a confirmed off-by-one row shift in the original transcription
    /// where each row N contained the correct paces for N+1.
    /// </summary>
    private static readonly SortedDictionary<int, PaceTableEntry> PaceTable = new()
    {
        // VDOT -> EasyMin(s/km), EasyMax(s/km), Marathon(s/km), Threshold(s/km), Interval(s/km), Repetition(s/km)
        // Easy min = faster end of easy range, Easy max = slower end
        // Paces are in seconds per kilometer.
        [30] = new(447, 497, 393, 370, 345, 325),
        [31] = new(437, 485, 384, 362, 337, 317),
        [32] = new(428, 474, 376, 354, 329, 310),
        [33] = new(419, 463, 368, 347, 322, 303),
        [34] = new(410, 453, 360, 340, 315, 297),
        [35] = new(401, 443, 352, 333, 309, 291),
        [36] = new(393, 434, 345, 326, 303, 285),
        [37] = new(385, 425, 338, 319, 297, 279),
        [38] = new(377, 417, 331, 313, 291, 274),
        [39] = new(370, 409, 325, 307, 286, 269),
        [40] = new(363, 401, 319, 301, 281, 264),
        [41] = new(356, 394, 313, 295, 276, 259),
        [42] = new(350, 387, 307, 290, 271, 255),
        [43] = new(344, 380, 302, 285, 266, 251),
        [44] = new(338, 374, 297, 280, 262, 247),
        [45] = new(332, 367, 292, 276, 258, 243),
        [46] = new(327, 361, 287, 271, 254, 239),
        [47] = new(321, 355, 282, 267, 250, 235),
        [48] = new(316, 349, 278, 263, 246, 232),
        [49] = new(311, 344, 273, 259, 242, 228),
        [50] = new(306, 338, 271, 255, 235, 218),
        [51] = new(301, 331, 267, 250, 231, 216),
        [52] = new(297, 327, 263, 247, 228, 213),
        [53] = new(293, 323, 260, 244, 225, 211),
        [54] = new(289, 319, 256, 241, 222, 208),
        [55] = new(285, 315, 253, 238, 219, 205),
        [56] = new(281, 311, 249, 235, 216, 203),
        [57] = new(277, 307, 246, 232, 213, 200),
        [58] = new(274, 303, 243, 229, 211, 198),
        [59] = new(270, 299, 240, 226, 208, 195),
        [60] = new(267, 295, 237, 224, 206, 193),
        [61] = new(264, 292, 234, 221, 203, 191),
        [62] = new(261, 288, 231, 219, 201, 189),
        [63] = new(258, 285, 228, 216, 199, 187),
        [64] = new(255, 282, 226, 214, 197, 185),
        [65] = new(252, 279, 223, 211, 195, 183),
        [66] = new(249, 276, 221, 209, 193, 181),
        [67] = new(247, 273, 218, 207, 191, 179),
        [68] = new(244, 270, 216, 205, 189, 177),
        [69] = new(242, 267, 214, 203, 187, 176),
        [70] = new(239, 265, 212, 201, 185, 174),
        [71] = new(237, 262, 210, 199, 183, 172),
        [72] = new(235, 259, 208, 197, 182, 170),
        [73] = new(232, 257, 206, 195, 180, 169),
        [74] = new(230, 254, 204, 193, 178, 167),
        [75] = new(228, 252, 202, 191, 177, 166),
        [76] = new(226, 250, 200, 190, 175, 164),
        [77] = new(224, 247, 198, 188, 174, 163),
        [78] = new(222, 245, 197, 186, 172, 161),
        [79] = new(220, 243, 195, 185, 171, 160),
        [80] = new(218, 241, 193, 183, 169, 159),
        [81] = new(216, 239, 192, 182, 168, 157),
        [82] = new(214, 237, 190, 180, 167, 156),
        [83] = new(212, 235, 189, 179, 165, 155),
        [84] = new(211, 233, 187, 178, 164, 154),
        [85] = new(209, 231, 186, 176, 163, 153),
    };

    /// <inheritdoc />
    public TrainingPaces CalculatePaces(decimal vdot)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vdot, 30m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vdot, 85m);

        var entry = InterpolateEntry(vdot);

        return new TrainingPaces(
            EasyPaceRange: new PaceRange(
                fast: Pace.FromSecondsPerKm(entry.EasyMinSeconds),
                slow: Pace.FromSecondsPerKm(entry.EasyMaxSeconds)),
            MarathonPace: Pace.FromSecondsPerKm(entry.MarathonSeconds),
            ThresholdPace: Pace.FromSecondsPerKm(entry.ThresholdSeconds),
            IntervalPace: Pace.FromSecondsPerKm(entry.IntervalSeconds),
            RepetitionPace: Pace.FromSecondsPerKm(entry.RepetitionSeconds));
    }

    /// <inheritdoc />
    public int EstimateMaxHr(int age)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(age, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 120);

        return 220 - age;
    }

    /// <summary>
    /// Interpolates between the two nearest table entries for non-integer VDOT values.
    /// For integer VDOT values, returns the exact table entry.
    /// </summary>
    private static PaceTableEntry InterpolateEntry(decimal vdot)
    {
        var lowerVdot = (int)Math.Floor(vdot);
        var upperVdot = (int)Math.Ceiling(vdot);

        // Clamp to table boundaries
        lowerVdot = Math.Clamp(lowerVdot, 30, 85);
        upperVdot = Math.Clamp(upperVdot, 30, 85);

        if (lowerVdot == upperVdot)
        {
            return PaceTable[lowerVdot];
        }

        var lower = PaceTable[lowerVdot];
        var upper = PaceTable[upperVdot];
        var fraction = (double)(vdot - lowerVdot);

        return new PaceTableEntry(
            EasyMinSeconds: Lerp(lower.EasyMinSeconds, upper.EasyMinSeconds, fraction),
            EasyMaxSeconds: Lerp(lower.EasyMaxSeconds, upper.EasyMaxSeconds, fraction),
            MarathonSeconds: Lerp(lower.MarathonSeconds, upper.MarathonSeconds, fraction),
            ThresholdSeconds: Lerp(lower.ThresholdSeconds, upper.ThresholdSeconds, fraction),
            IntervalSeconds: Lerp(lower.IntervalSeconds, upper.IntervalSeconds, fraction),
            RepetitionSeconds: Lerp(lower.RepetitionSeconds, upper.RepetitionSeconds, fraction));
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private sealed record PaceTableEntry(
        double EasyMinSeconds,
        double EasyMaxSeconds,
        double MarathonSeconds,
        double ThresholdSeconds,
        double IntervalSeconds,
        double RepetitionSeconds);
}
