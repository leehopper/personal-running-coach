namespace RunCoach.Api.Modules.Coaching.Onboarding.Legacy.Events.V1;

/// <summary>
/// Frozen V1 shape of <see cref="Onboarding.OnboardingStarted"/>. DO NOT MODIFY.
/// Touched only by the registered upcaster in
/// <see cref="Infrastructure.MartenConfiguration"/> and by the
/// <c>UpcasterRegressionTests</c> regression suite that asserts the upcasting
/// pipeline routes correctly. The current production shape is byte-identical
/// today; this V1 record exists to anchor the
/// <c>RunCoach.Domain.{Module}.Legacy.Events.V{N}</c> namespace convention
/// (DEC-067) and to give the synthetic-row regression a CLR target for
/// <c>mt_dotnet_type</c>.
/// </summary>
public sealed record OnboardingStarted(
    Guid UserId,
    DateTimeOffset StartedAt);
