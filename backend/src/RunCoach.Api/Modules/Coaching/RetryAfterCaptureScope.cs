namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Disposable scope returned by <see cref="RetryAfterCapture.BeginScope"/>. Clearing the scope
/// on dispose prevents a captured value from leaking across logical coaching-LLM calls.
/// </summary>
internal readonly struct RetryAfterCaptureScope : IDisposable
{
    /// <summary>Clears the active capture scope.</summary>
    public void Dispose() => RetryAfterCapture.ClearScope();
}
