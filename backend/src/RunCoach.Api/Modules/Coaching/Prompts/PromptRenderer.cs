namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Performs simple named-token replacement in context templates.
/// Replaces tokens of the form <c>{{token_name}}</c> with provided values.
/// No template engine dependency — conditional logic for which sections
/// to include stays in the ContextAssembler C# code.
/// </summary>
public static class PromptRenderer
{
    /// <summary>
    /// Renders a context template by replacing named tokens with their values.
    /// Tokens use the format <c>{{token_name}}</c>.
    /// Unmatched tokens are left in place (not removed).
    /// </summary>
    /// <param name="template">The context template containing named tokens.</param>
    /// <param name="tokens">
    /// A dictionary mapping token names (without braces) to their replacement values.
    /// For example, key "profile" replaces <c>{{profile}}</c> in the template.
    /// </param>
    /// <returns>The rendered template with all matching tokens replaced.</returns>
    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Count == 0)
        {
            return template;
        }

        var result = template;
        foreach (var (name, value) in tokens)
        {
            result = result.Replace("{{" + name + "}}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
