namespace RunCoach.Poc1.Console;

/// <summary>
/// Marker type used for user-secrets configuration binding.
/// The <c>UserSecretsId</c> is defined in the project file.
/// Referenced by <c>AddUserSecrets&lt;T&gt;</c> to locate the secrets store.
/// </summary>
internal sealed record AssemblyMarker(string Id)
{
    /// <summary>
    /// Default instance used as a type anchor for user-secrets discovery.
    /// </summary>
    public static readonly AssemblyMarker Default = new("runcoach-poc1-console");
}
