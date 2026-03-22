# Source: docs/specs/03-spec-eval-cache-ci/03-spec-eval-cache-ci.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Upgrade to xUnit v3

  Scenario: Test project builds successfully with xUnit v3 packages
    Given the test project references xunit.v3 instead of xunit v2 packages
    And Microsoft.NET.Test.Sdk and xunit.runner.visualstudio references have been removed
    When the developer runs "dotnet build" on the test project
    Then the build completes with exit code 0
    And zero errors or warnings are reported

  Scenario: All existing tests pass after xUnit v3 migration
    Given the test project has been migrated to xunit.v3 packages
    And no test source files have been modified beyond project file changes
    When the developer runs "dotnet test" on the test project
    Then all previously passing tests pass with exit code 0
    And Fact, Theory, Trait, and Skip attributes behave identically to v2

  Scenario: IClassFixture dependency injection continues to work
    Given SmokeTests.cs uses IClassFixture for shared test context
    And the test project uses xunit.v3
    When the developer runs "dotnet test --filter FullyQualifiedName~SmokeTests"
    Then all SmokeTests pass
    And the fixture is injected and initialized exactly once per test class

  Scenario: FluentAssertions and NSubstitute remain compatible
    Given the test project uses xunit.v3
    And FluentAssertions and NSubstitute package references are unchanged
    When the developer runs "dotnet test"
    Then tests using FluentAssertions assertions pass
    And tests using NSubstitute mocks pass
    And no package compatibility warnings appear in build output

  Scenario: TestContext.Current is accessible in xUnit v3
    Given the test project uses xunit.v3
    When a test method accesses TestContext.Current.CancellationToken
    Then the CancellationToken is a valid non-default token
    And it is automatically cancelled when the test exceeds its timeout
