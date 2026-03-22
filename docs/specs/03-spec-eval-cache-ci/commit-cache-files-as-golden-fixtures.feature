# Source: docs/specs/03-spec-eval-cache-ci/03-spec-eval-cache-ci.md
# Pattern: State + CLI/Process
# Recommended test type: Integration

Feature: Commit Cache Files as Golden Fixtures

  Scenario: Cache directory is tracked by git after gitignore update
    Given the .gitignore previously excluded poc1-eval-cache/
    When the developer removes the poc1-eval-cache/ exclusion from .gitignore
    And runs "git add backend/poc1-eval-cache/"
    Then git tracks all cache files under backend/poc1-eval-cache/
    And the poc1-eval-results/ exclusion remains in .gitignore

  Scenario: All sonnet cache scenarios are committed as golden fixtures
    Given the .gitignore no longer excludes poc1-eval-cache/
    When the developer runs "git ls-files backend/poc1-eval-cache/sonnet/cache/"
    Then 11 scenario directories are tracked
    And each directory contains an entry.json and contents.data file

  Scenario: All haiku cache scenarios are committed as golden fixtures
    Given the .gitignore no longer excludes poc1-eval-cache/
    When the developer runs "git ls-files backend/poc1-eval-cache/haiku/cache/"
    Then 5 scenario directories are tracked
    And each directory contains an entry.json and contents.data file

  Scenario: CI workflow sets Replay mode for eval tests
    Given the CI workflow at .github/workflows/ci.yml is configured
    When the backend test step executes
    Then the EVAL_CACHE_MODE environment variable is set to "Replay"
    And eval tests run without requiring an Anthropic API key

  Scenario: Replay mode ignores cache entry TTL expiration
    Given EVAL_CACHE_MODE is set to "Replay"
    And a cache entry has an expired TTL timestamp in its entry.json
    When the eval test loads the cached response
    Then the expired cache entry is served successfully
    And the test passes without consulting the real API
