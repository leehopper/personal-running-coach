# POC 1 — Clarifying Questions (Round 1)

## Q1: Implementation harness

**Question:** The POC 1 plan says "either through a simple .NET console app or through a test harness." Which approach?

**Answer:** Both — console app for exploratory prompt iteration, xUnit test harness for the eval suite.

## Q2: Eval automation level

**Question:** How automated should the quality scoring be at this stage?

**Answer:** Manual review + binary safety assertions. Tests assert hard constraints (paces in range, rest days present, no medical advice). Quality/tone scored manually by reviewing output.

## Q3: VDOT/pace computation

**Question:** Should the POC build real C# computation utilities or hardcode expected values?

**Answer:** Real computation — build VdotCalculator and PaceCalculator as reusable utilities that carry forward to MVP-0.

## Q4: Prompt file format and location

**Question:** Where should the system prompt and context injection template live?

**Answer:** Versioned YAML files (e.g., `backend/src/RunCoach.Api/Prompts/coaching-v1.yaml`).
