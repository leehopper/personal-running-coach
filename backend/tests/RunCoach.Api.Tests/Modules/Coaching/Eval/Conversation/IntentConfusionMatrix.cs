using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// A 3x3 confusion matrix over <see cref="MessageIntent"/> for the conversation
/// intent-classifier accuracy eval (Slice 4B Unit 7), indexed
/// <c>[expected, predicted]</c>. Reports overall accuracy and flags the
/// asymmetric "dangerous" misclassifications that DEC-085's bias-to-ask posture
/// exists to prevent — the analog of the adaptation suite's under-reaction hard
/// fail. The per-scenario theory is the strict zero-regression gate; this matrix
/// is the committed confusion-matrix proof artifact plus the aggregate
/// dangerous-cell safety net.
/// </summary>
internal sealed class IntentConfusionMatrix
{
    private static readonly MessageIntent[] Classes = [.. Enum.GetValues<MessageIntent>()];

    private readonly int[,] _matrix = new int[Classes.Length, Classes.Length];

    /// <summary>Gets the total number of scored predictions.</summary>
    internal int Total
    {
        get
        {
            var total = 0;
            foreach (var cell in _matrix)
            {
                total += cell;
            }

            return total;
        }
    }

    /// <summary>Gets the number of predictions on the diagonal (correct).</summary>
    internal int Correct
    {
        get
        {
            var correct = 0;
            for (var i = 0; i < Classes.Length; i++)
            {
                correct += _matrix[i, i];
            }

            return correct;
        }
    }

    /// <summary>Gets the overall accuracy in [0, 1] (1.0 when nothing was scored).</summary>
    internal double Accuracy => Total == 0 ? 1.0 : (double)Correct / Total;

    /// <summary>
    /// Gets the count of "dangerous" misclassifications under DEC-085's bias-to-ask
    /// asymmetry: predicting a confident class on a truly-<see cref="MessageIntent.Ambiguous"/>
    /// message (the classifier failed to ask), or answering a reported run as a
    /// question (<see cref="MessageIntent.WorkoutLog"/> ignored). The safe,
    /// over-cautious errors (predicting Ambiguous when the truth is Question or
    /// WorkoutLog) are NOT counted here — they ask rather than guess.
    /// </summary>
    internal int DangerousMisclassifications =>
        _matrix[(int)MessageIntent.Ambiguous, (int)MessageIntent.Question]
        + _matrix[(int)MessageIntent.Ambiguous, (int)MessageIntent.WorkoutLog]
        + _matrix[(int)MessageIntent.WorkoutLog, (int)MessageIntent.Question];

    /// <summary>Gets a value indicating whether any dangerous misclassification occurred.</summary>
    internal bool AnyDangerous => DangerousMisclassifications > 0;

    /// <summary>Records one scored prediction.</summary>
    /// <param name="expected">The ground-truth label.</param>
    /// <param name="predicted">The classifier's resolved intent.</param>
    internal void Record(MessageIntent expected, MessageIntent predicted) =>
        _matrix[(int)expected, (int)predicted]++;

    /// <summary>Builds a serialization-friendly snapshot for the eval-results proof artifact.</summary>
    /// <returns>An object suitable for JSON serialization.</returns>
    internal object ToSnapshot() => new
    {
        Rows = Classes.Select(expected => new
        {
            Expected = expected.ToString(),
            Predicted = Classes.ToDictionary(
                predicted => predicted.ToString(),
                predicted => _matrix[(int)expected, (int)predicted]),
        }),
        Total,
        Correct,
        Accuracy,
        DangerousMisclassifications,
    };
}
