# Research Prompt: Batch 6b — R-014
# .NET Libraries for LLM Response Caching, HTTP Recording/Replay, and Eval Tooling

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: .NET Libraries for LLM Response Caching, HTTP Recording/Replay, and Eval Tooling

Context: I have a .NET 10 / xUnit test suite that calls the Anthropic Claude API via the official Anthropic NuGet package (C# SDK). The eval tests generate training plans and safety responses, then assert on the output. Each API call costs ~$0.10 and takes 5-60 seconds. I need to cache/record API responses so that identical requests return cached results instantly, but changed prompts trigger fresh API calls. I also need structured output enforcement and LLM-as-judge capabilities.

What I'm looking for:

1. HTTP Recording/Replay Libraries for .NET

Libraries that intercept HTTP calls and record request/response pairs to disk, replaying them on subsequent runs when the request matches. This is the VCR pattern (like Ruby's VCR gem or Python's vcrpy). Specifically:
- What .NET libraries implement HTTP recording/replay? (e.g., Scotch, WireMock.Net, MockHttp, any others)
- How do they integrate with HttpClient / HttpMessageHandler in DI?
- Can they be configured to match on request body hash (not just URL) — important because all Anthropic API calls go to the same endpoint with different JSON bodies?
- Can they selectively bypass cache (e.g., force refresh via a flag or environment variable)?
- Do any of them work transparently with the Anthropic .NET SDK without modifying the SDK's HTTP client?
- What's the xUnit integration story — do they have test fixtures, base classes, or attributes?

2. LLM-Specific Test/Eval Libraries for .NET

Are there .NET libraries specifically designed for testing LLM applications?
- Braintrust C# SDK — what does it actually provide? Scoring, experiment tracking, caching?
- Semantic Kernel eval capabilities — Microsoft's SK has evaluation features, do they work standalone without the full SK orchestration?
- Any .NET ports or wrappers for DeepEval, Promptfoo, RAGAS concepts?
- NJsonSchema or JsonSchema.Net for validating structured output against a schema?
- Any libraries that provide LLM-as-judge scoring out of the box?

3. Anthropic Structured Outputs in .NET

- How to use Anthropic's constrained decoding / structured outputs with the official Anthropic C# NuGet package (version 12.x)?
- What's the exact API surface — is it output_config, tool_use, or something else?
- How to generate a JSON schema from C# record types and pass it to the API?
- Are there any .NET-specific examples or blog posts?
- What are the limitations (max properties, nesting depth, unsupported schema features)?

4. Anthropic Prompt Caching

- Anthropic has a prompt caching feature that caches the system prompt server-side. How does this work with the .NET SDK?
- What are the cost savings (the research mentions cache reads at 0.1x base price)?
- How does this interact with structured outputs?
- Is there a way to leverage this for eval tests where the system prompt is identical across all test cases?

5. Test Data Management Patterns

- Approval testing / snapshot testing in .NET (Verify, ApprovalTests.Net, Snapshooter) — can these be adapted for LLM response snapshots?
- Golden file / fixture management — any .NET patterns for maintaining a set of approved LLM responses that tests assert against, with a workflow for updating them when prompts change?
- How do teams handle the "invalidate cache when prompt changes" problem specifically?

6. Anything Else I'm Missing

- Are there .NET source generators or Roslyn analyzers for LLM eval?
- Any Polly-based patterns for LLM call resilience + caching combined?
- Does the Anthropic .NET SDK support the Batch API (50% discount for non-real-time)?
- Are there test parallelization concerns with LLM rate limits, and libraries that handle this?
- Any .NET-native NLI (Natural Language Inference) libraries or ONNX model runners that could be used for cheap semantic assertions?

Constraints:
- .NET 10 / C# 14
- xUnit + FluentAssertions
- Official Anthropic NuGet package (not a custom HTTP client)
- Must work in a local dev environment (no cloud infrastructure required)
- Prefer well-maintained packages with recent activity over abandoned projects

Output I need:
- A ranked recommendation of libraries with NuGet package names and version info
- For HTTP recording specifically: a concrete example of how to wire it into xUnit tests that use the Anthropic SDK
- For structured outputs: a concrete example of defining a C# record and getting guaranteed schema-compliant JSON from Claude
- Comparison table of approaches with columns: library name, what it solves, maturity/maintenance status, .NET version support, LLM-specific features
- Links to source repos, docs, and any blog posts showing real-world usage with LLM APIs
