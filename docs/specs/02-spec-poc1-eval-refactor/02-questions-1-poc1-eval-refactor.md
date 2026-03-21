# Questions — Round 1: POC 1 Eval Refactor

## Q1: YAML Loading Approach
**Asked:** How should YAML loading work (runtime vs build-time)?
**Answer:** Research first. Agent completed — recommends runtime loading via `IPromptStore` singleton with Scriban templating, `ConcurrentDictionary` cache, file-watching in dev. Two-part prompt assembly (static cacheable prefix + dynamic context) for Anthropic prompt caching.

## Q2: Eval Test Execution
**Asked:** Should eval tests actually run against live API or just be structurally refactored?
**Answer:** Running with caching. API key set up via user-secrets on orchestration branch. M.E.AI.Evaluation.Reporting disk cache means only first run calls the API.

## Q3: Schema Design
**Asked:** Should response records match current informal JSON or be redesigned?
**Answer:** Full redesign. Agent completed — recommends three separate schemas (MacroPlan, MesoWeek, MicroWorkouts), coaching narrative as separate unstructured call, paces as integer seconds/km, enums for all categorical values. All schemas verified within constrained decoding limits (<=30 props, <=3 nesting levels).
