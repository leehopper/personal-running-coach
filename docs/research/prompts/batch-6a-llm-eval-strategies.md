# Research Prompt: Batch 6a — R-013
# LLM Eval Test Strategies for Non-Deterministic Output Assertion

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: LLM Eval Test Strategies for Non-Deterministic Output Assertion

Context: I'm building an eval suite for an AI coaching application (running coach). The system generates training plans via Claude API calls, and I need to assert that the outputs meet safety constraints, contain correct structured data, and respect user profile constraints. The outputs are non-deterministic — same prompt produces different JSON schemas, different phrasing, different structure each run. My current approach uses string matching and exact JSON key lookups, which is brittle.

Specific problems I'm facing:
1. LLM returns JSON with varying key names/nesting across runs (e.g., macro_plan vs training_plan vs plan)
2. Safety assertions rely on keyword presence/absence in natural language (e.g., checking the coach "defers to a medical professional" by looking for the word "doctor")
3. Numeric assertions (pace ranges, weekly volume) depend on parsing structured output that changes shape
4. Need to balance test reliability (low false-failure rate) against test strictness (catching real regressions)

What I want to learn:
1. How do teams at Anthropic, OpenAI, Google DeepMind, and LLM startups structure their eval suites? What frameworks exist (e.g., OpenAI Evals, Braintrust, Promptfoo, DeepEval, RAGAS, LangSmith)? How do they handle non-determinism?
2. LLM-as-judge pattern: Using a second LLM call to evaluate whether the first LLM's output meets criteria semantically rather than syntactically. What are the best practices, pitfalls, and cost tradeoffs? How do you prevent the judge from being too lenient?
3. Structured output enforcement: What techniques exist to guarantee JSON schema compliance from LLMs? (tool use / function calling, JSON mode, constrained decoding, Anthropic's tool_use for structured output). How reliable are these in practice?
4. Statistical approaches to flaky eval tests: pass-K-of-N strategies, confidence intervals, consensus voting across multiple runs. What's the industry standard for acceptable flake rate?
5. Assertion strategies beyond string matching: semantic similarity scoring, embedding-based checks, rubric-based grading, regex with tolerance, NLI (natural language inference) for entailment checking.
6. Real-world eval architectures: How do companies like Guardrails AI, Galileo, Arthur AI approach output validation? What about academic work on LLM evaluation frameworks?
7. Cost-aware eval design: How do teams balance eval thoroughness against API cost? Caching strategies, deterministic seeding (temperature=0), snapshot-based testing.

Output I need:
- A ranked list of approaches with tradeoffs (reliability vs cost vs complexity)
- Concrete framework recommendations for a .NET/xUnit codebase calling the Anthropic API
- Specific examples of how to assert "the LLM deferred to a medical professional" without brittle keyword matching
- Whether structured output enforcement (Anthropic tool_use) can eliminate the JSON parsing problem entirely
- Links to source material, documentation, and reference implementations
