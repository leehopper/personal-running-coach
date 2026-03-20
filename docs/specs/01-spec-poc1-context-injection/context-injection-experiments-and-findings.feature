# Source: docs/specs/01-spec-poc1-context-injection/01-spec-poc1-context-injection.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Context Injection Experiments and Findings

  Scenario: Token budget experiment compares plan quality across context sizes
    Given the Lee profile is loaded as the baseline
    And prompt variations exist for approximately 8K, 12K, and 15K total context tokens
    When each token budget variation is run through the console app
    Then each variation produces a complete training plan
    And the findings document records methodology and quality observations for each budget level

  Scenario: Positional placement experiment compares context layouts
    Given the Lee profile is loaded as the baseline
    And prompt variations exist for profile-at-start, profile-at-end, and profile-in-middle
    When each positional variation is run through the console app
    Then each variation produces a complete training plan
    And the "profile-at-start" variation produces fewer hallucinated or incorrect profile details than "profile-at-end"
    And the findings document records which layout produced the most accurate profile data usage

  Scenario: Summarization level experiment compares history representations
    Given a profile with 3 weeks of training history is loaded
    And prompt variations exist for per-workout history and weekly summary history
    When each summarization variation is run through the console app
    Then each variation produces a complete training plan
    And the findings document records which summarization level produced better plan adaptation

  Scenario: Conversation history experiment compares turn counts
    Given the Lee profile is loaded as the baseline
    And prompt variations exist for 0 turns and 5 turns of prior conversation context
    When each conversation history variation is run through the console app
    Then each variation produces a complete training plan
    And the findings document records the impact of conversation context on plan coherence

  Scenario: Cross-validation with additional profile
    Given at least one experiment uses a second profile beyond Lee for cross-validation
    When the cross-validation experiment runs
    Then the findings document includes observations from both the Lee profile and the additional profile

  Scenario: Findings document covers all required sections
    Given all 4 experiments have been completed
    When the findings document is written to the spec directory
    Then the document contains a section on what worked and what did not for each experiment
    And the document contains a recommended context injection strategy for MVP-0
    And the document contains prompt engineering lessons learned
    And the document contains any architecture decisions needing revision
    And the document contains token usage observations and cost estimates at scale

  Scenario: Multiple prompt YAML versions demonstrate iteration
    Given experiments have been conducted and findings recorded
    When the prompt files directory is examined
    Then at least 2 versioned prompt YAML files exist
    And the versions reflect iteration based on experimental findings
