using Microsoft.Extensions.Options;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Source-generated validator for <see cref="JwtAuthOptions"/>. The
/// <c>[OptionsValidator]</c> generator rewrites each DataAnnotation attribute
/// into a reflection-free check so AOT publish stops warning IL2025 / IL3050.
/// </summary>
[OptionsValidator]
public sealed partial class JwtAuthOptionsValidator : IValidateOptions<JwtAuthOptions>
{
}
