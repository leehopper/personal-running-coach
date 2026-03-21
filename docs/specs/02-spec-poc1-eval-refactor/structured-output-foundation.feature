# Source: docs/specs/02-spec-poc1-eval-refactor/02-spec-poc1-eval-refactor.md
# Pattern: API + Unit
# Recommended test type: Unit + Integration

Feature: Structured Output Foundation

  Scenario: Schema generation produces valid JSON schema with additionalProperties false
    Given the structured output record types MacroPlanOutput, MesoWeekOutput, and MicroWorkoutListOutput are defined
    When JsonSchemaHelper generates a JSON schema for each record type
    Then each generated schema is valid JSON
    And every object node in each schema contains "additionalProperties": false
    And property descriptions from Description attributes appear in the schema

  Scenario: Schema respects property count and nesting depth limits
    Given the structured output record types are defined with Anthropic constraints
    When JsonSchemaHelper generates schemas for MacroPlanOutput, MesoWeekOutput, and MicroWorkoutListOutput
    Then no object in any schema has more than 30 properties
    And no schema has a nesting depth greater than 3

  Scenario: Structured output records round-trip through JSON serialization
    Given a MacroPlanOutput instance with populated phases and pace values in seconds per kilometer
    When the instance is serialized to JSON and deserialized back to MacroPlanOutput
    Then the deserialized record is equivalent to the original
    And all enum values serialize as strings rather than integers

  Scenario: MesoWeekOutput round-trips with all seven day slots preserved
    Given a MesoWeekOutput instance with 7 day slots including rest and workout days
    When the instance is serialized to JSON and deserialized back to MesoWeekOutput
    Then all 7 day slots are present in the deserialized output
    And each day slot retains its DaySlotType value

  Scenario: MicroWorkoutListOutput round-trips with nested segments preserved
    Given a MicroWorkoutListOutput instance with workouts containing multiple segments
    When the instance is serialized to JSON and deserialized back to MicroWorkoutListOutput
    Then all workouts and their nested segments are present in the deserialized output
    And segment SegmentType and IntensityProfile enum values are preserved as strings

  Scenario: GenerateStructuredAsync returns typed MacroPlanOutput from Anthropic API
    Given the Anthropic API key is configured
    And a system prompt and the Lee athlete profile user message are prepared
    When GenerateStructuredAsync of MacroPlanOutput is called with the system prompt and user message
    Then the response deserializes to a MacroPlanOutput record
    And the record contains at least one non-null phase
    And all pace values are positive integers representing seconds per kilometer
