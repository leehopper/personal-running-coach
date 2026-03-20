# Source: docs/specs/01-spec-poc1-context-injection/01-spec-poc1-context-injection.md
# Pattern: CLI/Process + API + State
# Recommended test type: Integration

Feature: Coaching Prompt & Context Assembly

  Scenario: Context assembler builds prompt payload from profile data
    Given a complete user profile with UserProfile, GoalState, FitnessEstimate, and TrainingPaces
    And optional training history of 3 weeks
    And optional conversation history of 2 turns
    When the ContextAssembler builds the prompt payload
    Then the assembled payload contains the user profile data in the stable prefix section
    And the assembled payload contains training history in the variable middle section
    And the assembled payload contains conversation history in the conversational end section
    And no profile fields are missing or contain placeholder values

  Scenario: Context assembler stays within token budget
    Given a complete user profile with UserProfile, GoalState, FitnessEstimate, and TrainingPaces
    And maximum training history of 4 weeks of per-workout data
    And conversation history of 10 turns
    When the ContextAssembler builds the prompt payload
    Then the total estimated token count is under 15000

  Scenario: Context assembler handles profile with no training history
    Given a beginner user profile with no prior training history
    And no conversation history
    When the ContextAssembler builds the prompt payload
    Then the assembled payload contains the user profile data
    And the training history section is empty or omitted
    And the payload is valid for submission to the LLM

  Scenario: Coaching system prompt loads from versioned YAML file
    Given the prompt file "coaching-v1.yaml" exists in the Prompts directory
    When the system loads the coaching prompt
    Then the loaded prompt includes coaching persona directives
    And the loaded prompt includes safety rules
    And the loaded prompt includes output format specification
    And the loaded prompt includes context injection template markers

  Scenario: Console app generates training plan for a named profile
    Given the Anthropic API key is configured via user-secrets
    And the console app project is built
    When the user runs "dotnet run --project RunCoach.Poc1.Console -- --profile lee"
    Then the command exits with code 0
    And stdout contains a MacroPlan with training phases
    And stdout contains a MesoWeek template for the current week
    And stdout contains MicroWorkout details for the next 3 days

  Scenario: Console app accepts prompt-version flag for A/B iteration
    Given the Anthropic API key is configured via user-secrets
    And prompt files "coaching-v1.yaml" and "coaching-v2.yaml" exist
    When the user runs "dotnet run --project RunCoach.Poc1.Console -- --profile lee --prompt-version v2"
    Then the command exits with code 0
    And the system uses the "coaching-v2.yaml" prompt file for the LLM call
    And stdout contains a generated training plan

  Scenario: Console app rejects unknown profile name
    Given the console app project is built
    When the user runs "dotnet run --project RunCoach.Poc1.Console -- --profile unknown"
    Then the command exits with a non-zero exit code
    And stderr contains an error message indicating the profile name is not recognized

  Scenario: API key not configured produces clear error
    Given no Anthropic API key is configured
    When the user runs "dotnet run --project RunCoach.Poc1.Console -- --profile lee"
    Then the command exits with a non-zero exit code
    And stderr contains an error message indicating the API key is missing
    And no partial request is sent to the Anthropic API
