# AI Running Coach — Planning Hub

High-level planning documents for an AI-powered adaptive running coach. This collection captures vision, architecture thinking, feature ideas, and open questions as the project moves toward PRD stage.

## Structure

```
planning/
  vision-and-principles.md   — Why this exists, what makes it different, design principles
  interaction-model.md       — Three interaction modes: onboarding, proactive coaching, open conversation
  planning-architecture.md   — Macro/meso/micro tiered plan structure
  self-optimization.md       — How the system learns and adapts from real-world feedback
  memory-and-architecture.md — Agent memory model and context injection strategy
  poc-roadmap.md             — POC experiments that need to happen before building

features/
  backlog.md                 — Feature ideas organized by priority horizon (MVP → Future)

decisions/
  open-questions.md          — Unresolved questions organized by domain
  decision-log.md            — Decisions made with rationale and alternatives considered

research/
  research-queue.md          — Topics queued for deep research with status tracking
  artifacts/                 — Full research outputs (keep detail here, not in planning docs)
```

## How to Use These Docs

- **Adding a new idea?** Drop it in `features/backlog.md` under the right priority section.
- **Have a question that needs answering?** Add it to `decisions/open-questions.md`.
- **Made a decision?** Update the question status and add an entry to `decisions/decision-log.md`.
- **Running a POC?** Update status and findings in `planning/poc-roadmap.md`.
- **Need deeper research?** Add it to `research/research-queue.md`, hand off to a research agent, and store the full output in `research/artifacts/`. Only pull key takeaways back into planning docs.

## What's Next

These documents are pre-PRD. The next stage is to work through the POC roadmap, resolve key open questions, and then synthesize findings into a proper product requirements document.
