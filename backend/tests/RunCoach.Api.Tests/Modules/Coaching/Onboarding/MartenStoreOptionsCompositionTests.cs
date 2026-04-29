using System.Reflection;
using FluentAssertions;
using Marten.EntityFrameworkCore;
using Marten.Metadata;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Pure type-system regression guards for the DEC-061 fix-up that unblocked
/// the integration-test path. Cheapest verification layer for the exact
/// failure modes documented in R-070:
/// <list type="bullet">
/// <item><description>If <see cref="UserProfileFromOnboardingProjection"/> is
///   ever "simplified" back to a non-EF base (e.g. <c>SingleStreamProjection&lt;RunnerOnboardingProfile&gt;</c>),
///   Marten's <c>DocumentMapping.CompileAndValidate()</c> will fail at host
///   start because <see cref="RunnerOnboardingProfile.UserId"/> is the PK rather than
///   <c>Id</c> / <c>id</c>. Asserting the EF base class signature catches
///   that regression at compile / unit-test time.</description></item>
/// <item><description>If <see cref="RunnerOnboardingProfile"/> ever drops its
///   <see cref="ITenanted"/> implementation, Marten's
///   <c>EfCoreSingleStreamProjection.ValidateConfiguration</c> override hard-fails
///   the host start with <c>InvalidProjectionException</c> whenever the event
///   store uses <c>TenancyStyle.Conjoined</c>.</description></item>
/// </list>
/// The full host-boot path is also covered by
/// <c>StartupSmokeIntegrationTests.SutHost_MartenDocumentStore_Resolves_And_Opens_Session</c>,
/// but those tests pay the WebApplicationFactory + Testcontainers cost. These
/// type-system assertions run in milliseconds and pin the invariants that
/// make the host boot in the first place.
/// </summary>
public sealed class MartenStoreOptionsCompositionTests
{
    [Fact]
    public void UserProfileFromOnboardingProjection_Inherits_EfCoreSingleStreamProjection()
    {
        // Arrange / Act
        var actualBase = typeof(UserProfileFromOnboardingProjection).BaseType;

        // Assert — the EF base class is the load-bearing piece. Marten.EntityFrameworkCore's
        // `opts.Add(IProjection, ProjectionLifecycle)` extension method short-circuits
        // the standard projection-storage pipeline only for projections derived from
        // `EfCoreSingleStreamProjection<TDoc, TId, TDbContext>`. Reverting to a plain
        // `SingleStreamProjection<UserProfile>` would silently re-route writes through
        // Marten's document storage and break atomicity with the EF row.
        actualBase.Should().Be<EfCoreSingleStreamProjection<RunnerOnboardingProfile, Guid, RunCoachDbContext>>(
            because: "DEC-061 / R-070 — the EF base class is what makes opts.Add(...) wire the projection as a transaction participant on the same Npgsql connection as the Marten event append");
    }

    [Fact]
    public void RunnerOnboardingProfile_Implements_ITenanted()
    {
        // Arrange / Act / Assert — Conjoined tenancy hard-requires ITenanted on every
        // EF projection target. Dropping the interface re-triggers
        // InvalidProjectionException at host start.
        typeof(ITenanted).IsAssignableFrom(typeof(RunnerOnboardingProfile)).Should().BeTrue(
            because: "Marten's EfCoreSingleStreamProjection.ValidateConfiguration fails the host start with InvalidProjectionException if the EF target type does not implement ITenanted under TenancyStyle.Conjoined (Slice 0 default)");
    }

    [Fact]
    public void RunnerOnboardingProfile_Has_Nullable_TenantId_Property()
    {
        // Arrange / Act
        var actualProperty = typeof(RunnerOnboardingProfile).GetProperty(nameof(ITenanted.TenantId));

        // Assert — the nullable string contract matches Marten.Metadata.ITenanted's
        // member shape. The migration `AddUserProfileTenantId` adds the column as
        // nullable text; flipping the column to non-null would break existing rows
        // seeded before the fix landed (the projection backfills on the next applied
        // event). PropertyType.Should().Be<string>() returns System.String for both
        // string and string?; NullabilityInfoContext is what reads the compiler's
        // nullable-reference metadata, so the read/write nullability assertions below
        // are what actually pin the contract.
        actualProperty.Should().NotBeNull();
        actualProperty!.PropertyType.Should().Be<string>();
        actualProperty.CanRead.Should().BeTrue();
        actualProperty.CanWrite.Should().BeTrue();

        var nullability = new NullabilityInfoContext().Create(actualProperty);
        nullability.ReadState.Should().Be(NullabilityState.Nullable);
        nullability.WriteState.Should().Be(NullabilityState.Nullable);
    }
}
