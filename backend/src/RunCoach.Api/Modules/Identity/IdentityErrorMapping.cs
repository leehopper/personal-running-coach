namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// The output of <see cref="IdentityErrorCodeMapper.Map"/> — a pair of the
/// DTO-property bucket name the error should surface under and the HTTP
/// semantic class the error represents. Kept as a dedicated type in its
/// own file per the project one-type-per-file rule.
/// </summary>
public readonly record struct IdentityErrorMapping(string PropertyName, IdentityErrorKind Kind);
