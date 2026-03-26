namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Performs simple named-token replacement in context templates.
/// Replaces tokens of the form <c>{{token_name}}</c> with provided values.
/// No template engine dependency — conditional logic for which sections
/// to include stays in the ContextAssembler C# code.
/// </summary>
/// <remarks>
/// Token values are sanitized before substitution: any <c>{{</c> or <c>}}</c>
/// sequences in values are collapsed to single braces to prevent template
/// injection (a substituted value cannot introduce new token placeholders).
/// </remarks>
public static class PromptRenderer
{
    /// <summary>
    /// Renders a context template by replacing named tokens with their values.
    /// Tokens use the format <c>{{token_name}}</c>.
    /// Unmatched tokens are left in place (not removed).
    /// Token values are sanitized to prevent template injection — any <c>{{</c>
    /// or <c>}}</c> sequences in values are collapsed to single braces.
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
            result = result.Replace("{{" + name + "}}", SanitizeTokenValue(value), StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Sanitizes a token replacement value by repeatedly collapsing double-brace
    /// sequences (<c>{{</c> and <c>}}</c>) to single braces until none remain.
    /// A single pass is insufficient because odd-count brace runs (e.g. <c>{{{</c>)
    /// can produce new double-brace pairs after one collapse. The loop guarantees
    /// the result contains no <c>{{</c> or <c>}}</c>, so substituted values
    /// cannot introduce new token placeholders (template injection).
    /// </summary>
    /// <param name="value">The raw token value to sanitize.</param>
    /// <returns>The sanitized value safe for template substitution.</returns>
    internal static string SanitizeTokenValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = value;
        while (result.Contains("{{", StringComparison.Ordinal)
            || result.Contains("}}", StringComparison.Ordinal))
        {
            result = result
                .Replace("{{", "{", StringComparison.Ordinal)
                .Replace("}}", "}", StringComparison.Ordinal);
        }

        return result;
    }
}
