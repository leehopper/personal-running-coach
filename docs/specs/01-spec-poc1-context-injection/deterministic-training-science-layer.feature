# Source: docs/specs/01-spec-poc1-context-injection/01-spec-poc1-context-injection.md
# Pattern: CLI/Process + Unit (pure computation)
# Recommended test type: Unit

Feature: Deterministic Training Science Layer

  Scenario: VDOT computed from 5K race time
    Given a runner with a 5K race time of 20 minutes 0 seconds
    When the VDOT calculator processes the race result
    Then the computed VDOT value is within 0.5 of the published Daniels' table value for that time

  Scenario: VDOT computed from 10K race time
    Given a runner with a 10K race time of 42 minutes 0 seconds
    When the VDOT calculator processes the race result
    Then the computed VDOT value is within 0.5 of the published Daniels' table value for that time

  Scenario: VDOT computed from half-marathon race time
    Given a runner with a half-marathon race time of 1 hour 32 minutes
    When the VDOT calculator processes the race result
    Then the computed VDOT value is within 0.5 of the published Daniels' table value for that time

  Scenario: VDOT computed from marathon race time
    Given a runner with a marathon race time of 3 hours 15 minutes
    When the VDOT calculator processes the race result
    Then the computed VDOT value is within 0.5 of the published Daniels' table value for that time

  Scenario: No race history yields null VDOT
    Given a runner with no race history
    When the VDOT calculator attempts to compute VDOT
    Then the result is null indicating insufficient data

  Scenario: Training pace zones derived from known VDOT
    Given a VDOT value of 50
    When the pace calculator derives training zones
    Then the easy pace range is returned as a min/max per-km value
    And the marathon pace range is returned as a min/max per-km value
    And the threshold pace range is returned as a min/max per-km value
    And the interval pace range is returned as a min/max per-km value
    And the repetition pace range is returned as a min/max per-km value
    And all pace ranges fall within the published Daniels' table values for VDOT 50

  Scenario: Estimated max HR fallback when no measured value
    Given a runner with no measured maximum heart rate
    When the fitness estimate is computed
    Then the system uses the age-based estimated max HR formula as a fallback

  Scenario: All five test profiles contain complete data
    Given the test profile data set is loaded
    When each profile is validated against the required schema
    Then all 5 profiles contain a UserProfile with name and experience level
    And all 5 profiles contain a GoalState
    And all 5 profiles contain a FitnessEstimate
    And all 5 profiles contain TrainingPaces derived from their VDOT
    And profiles Lee, Maria, James, and Priya include 2-4 weeks of simulated training history
    And profile Sarah has no prior training history
