namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests that hit the shared Testcontainers
/// Postgres. On dispose, Respawn wipes the <c>public</c> schema so each test
/// starts clean. Downstream tests that touch Marten streams should override
/// <see cref="DisposeAsync"/> and additionally call
/// <c>Factory.Services.ResetAllMartenDataAsync()</c> on the host they build.
/// </summary>
public abstract class DbBackedIntegrationTestBase(RunCoachAppFactory factory) : IAsyncLifetime
{
    protected RunCoachAppFactory Factory { get; } = factory;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public virtual async ValueTask DisposeAsync()
    {
        await Factory.ResetPublicSchemaAsync();
        GC.SuppressFinalize(this);
    }
}
