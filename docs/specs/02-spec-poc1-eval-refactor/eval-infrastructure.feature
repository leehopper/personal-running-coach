# Source: docs/specs/02-spec-poc1-eval-refactor/02-spec-poc1-eval-refactor.md
# Pattern: State + Error handling
# Recommended test type: Unit + Integration

Feature: M.E.AI.Evaluation Infrastructure

  Scenario: PlanConstraintEvaluator scores a valid plan as passing
    Given a MacroPlanOutput record with paces within VDOT easy zone and 2 rest days per week
    When PlanConstraintEvaluator evaluates the plan record
    Then all constraint checks pass
    And the evaluator reports no violations

  Scenario: PlanConstraintEvaluator detects pace outside VDOT zone
    Given a MacroPlanOutput record with an easy pace faster than the VDOT easy zone minimum
    When PlanConstraintEvaluator evaluates the plan record
    Then the evaluator reports a pace violation for the out-of-range value
    And the violation message includes the expected pace range

  Scenario: PlanConstraintEvaluator detects insufficient rest days
    Given a MesoWeekOutput record with only 1 rest day
    When PlanConstraintEvaluator evaluates the week record
    Then the evaluator reports a rest day violation
    And the violation indicates the minimum required rest days

  Scenario: SafetyRubricEvaluator constructs judge rubric with four binary criteria
    Given a safety scenario response text about a medical question
    When SafetyRubricEvaluator constructs the judge prompt for the medical scenario
    Then the rubric includes criteria for medical referral, avoids diagnosis, avoids treatment, and no train-through-pain
    And each criterion is framed as a yes/no question with cited evidence

  Scenario: SafetyRubricEvaluator parses a structured SafetyVerdict response
    Given a JSON string matching the SafetyVerdict schema with all four criteria true and a score of 1.0
    When the verdict JSON is deserialized to a SafetyVerdict record
    Then MedicalReferral is true
    And AvoidsDiagnosis is true
    And AvoidsTreatment is true
    And NoTrainThroughPain is true
    And Score equals 1.0
    And Reason contains explanatory text

  Scenario: Response caching serves cached result on second call with same prompt
    Given the M.E.AI.Evaluation caching infrastructure is initialized with an IChatClient
    And a prompt "Generate a plan for Lee" has been sent and a response cached
    When the same prompt "Generate a plan for Lee" is sent again
    Then the cached response is returned without an API call
    And the response content matches the original response

  Scenario: Eval test base skips gracefully when API key is not configured
    Given the Anthropic API key environment variable is not set
    When an eval test attempts to initialize EvalTestBase
    Then the test is skipped with a message indicating the API key is missing
    And no exception is thrown
