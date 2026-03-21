# Source: docs/specs/02-spec-poc1-eval-refactor/02-spec-poc1-eval-refactor.md
# Pattern: State + CLI
# Recommended test type: Unit + Integration

Feature: YAML Prompt Store with Scriban Templating

  Scenario: YamlPromptStore loads and deserializes a prompt YAML file
    Given a valid YAML prompt file "coaching-system.v1.yaml" exists in the Prompts directory
    When YamlPromptStore loads prompt templates at startup
    Then the prompt with id "coaching-system" and version "v1" is available
    And the loaded template contains a non-empty StaticSystemPrompt
    And the loaded template contains a non-empty ContextTemplate

  Scenario: YamlPromptStore selects the active version from configuration
    Given prompt files "coaching-system.v1.yaml" and "coaching-system.v2.yaml" exist
    And appsettings configures the active version for "coaching-system" as "v1"
    When GetActiveVersionAsync is called for "coaching-system"
    Then the returned version is "v1"

  Scenario: YamlPromptStore validates that all configured versions exist at startup
    Given appsettings configures the active version for "coaching-system" as "v99"
    And no file "coaching-system.v99.yaml" exists in the Prompts directory
    When YamlPromptStore attempts to load prompt templates
    Then an error is reported indicating the configured version "v99" was not found

  Scenario: Repeated loads return cached templates without re-reading disk
    Given YamlPromptStore has already loaded "coaching-system.v1.yaml"
    When GetPromptAsync is called again for "coaching-system" version "v1"
    Then the same template instance is returned from the in-memory cache
    And no additional file I/O occurs

  Scenario: Scriban renders athlete context into the context template
    Given a ContextTemplate containing Scriban placeholders for athlete name and weekly mileage
    And an athlete profile with name "Lee" and weekly mileage 40
    When ContextAssembler renders the template with the athlete profile
    Then the rendered output contains "Lee" and "40"
    And no unresolved Scriban placeholders remain in the output

  Scenario: Assembled prompt splits into static and dynamic parts for caching
    Given a loaded prompt template with StaticSystemPrompt and ContextTemplate
    And an athlete profile for rendering
    When ContextAssembler assembles the full prompt
    Then the result contains a static prefix intended for Anthropic cache_control
    And the result contains a dynamic suffix with rendered athlete context
    And the static prefix does not contain any athlete-specific data

  Scenario: Console app produces output using YAML-loaded prompts
    Given the Anthropic API key is configured
    And the YAML prompt files are in place
    When the command "dotnet run --project RunCoach.Poc1.Console -- --profile lee" is executed
    Then the command exits with code 0
    And stdout contains coaching output generated from the YAML-based prompt
