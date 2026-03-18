# Research Prompt: Batch 1 — R-008 + R-009
# Claude Code Best Practices, Plugins, and AI-Assisted Side Project Workflow

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

I'm a solo developer building an AI-powered running coach application as a side project. I'll be building the entire thing with Claude Code, and I need to understand the current state of the art for maximizing what Claude Code can do — not just as a coding assistant, but as the backbone of my entire development workflow.

### About the project

This is a web app that uses an LLM to build, maintain, and continuously optimize personalized running training plans through conversation. It's pre-build — I have a structured set of high-level planning documents (vision, interaction model, planning architecture, feature backlog, open questions, decision log, POC roadmap, and a research queue). My next steps are POC work, then MVP, then iteration.

Key context about how I work:
- This is a side project with limited time. I need to maximize what autonomous agents accomplish per session.
- I've used Claude Code professionally with proprietary plugins/skills, but for this personal project I need to find what's available in the open-source / community ecosystem.
- My planning docs are in a `running-app-org/` folder with subdirectories: `planning/`, `features/`, `decisions/`, `research/`. These are the "source of truth" for the project and need to stay clean and current as the project evolves through phases.

### What I need researched

**1. Claude Code ecosystem survey (R-008)**

Research the current landscape of Claude Code tools, plugins, skills, frameworks, and community resources. Specifically:

- **Community resources:** What are "everything claude code," "awesome-claude-code," and similar curated lists? What's actually useful vs. hype? Are there other key resources, blogs, or communities I should be aware of?
- **Plugins and skills:** What plugins and custom skills are available for Claude Code that would be relevant for a solo developer building a full-stack web app? Focus on what's available for personal/open-source use.
- **MCP servers:** What MCP servers are commonly used with Claude Code for web app development? Which ones are essential vs. nice-to-have?
- **Agent patterns:** What multi-agent, agent team, or agent orchestration patterns exist for Claude Code? (e.g., claude-flow, agent-stack, or similar tools). How mature are they? Are they production-ready or experimental?
- **CLAUDE.md best practices:** What are the current best practices for structuring CLAUDE.md files? What goes in them? How do experienced users structure them for complex projects?

**2. AI-assisted side project workflow (R-009)**

This is the harder, more important question. Research how solo builders and side project developers organize AI-assisted development across project phases:

- **Phase-to-phase context handoff:** How do people maintain continuity when a project moves from planning → POC → MVP → post-MVP? How does a fresh Claude Code session get enough context to work autonomously without re-explaining everything? What patterns exist for this (e.g., structured CLAUDE.md, context documents, session bootstrapping)?
- **Separating ideation from implementation:** During active development, new ideas and feature thoughts will keep coming. How do people keep these from polluting the codebase while still capturing them? Are there workflows or conventions that make this lightweight rather than a process burden?
- **Repo structure for AI-assisted development:** Are there emerging conventions for how to structure a project repo so that Claude Code (or similar AI tools) can navigate and understand it effectively? Does the repo structure itself need to be optimized for agent consumption?
- **Autonomous agent effectiveness:** What guardrails, task scoping patterns, or "preflight checklist" conventions produce good outcomes when running Claude Code agents with limited supervision? What are the common failure modes and how do experienced users prevent them?
- **Multi-workspace or multi-context setups:** How do people handle having a planning/docs workspace separate from a code workspace? Do they live in the same repo? Different repos? Is there a best practice here?

### What "good" looks like for this research

I want actionable findings, not a literature review. For each tool, framework, or pattern you find:
- What is it and what does it actually do?
- How mature is it? (experimental / actively maintained / widely adopted)
- Is it relevant to my specific situation (solo dev, side project, full-stack web app, phased development)?
- What's the setup cost vs. ongoing value?

Prioritize depth over breadth — I'd rather understand 5 things well than get a list of 30 things with one-line descriptions.

### Output format

Structure your findings as a single research document with two major sections (matching R-008 and R-009). Within each, organize by sub-topic. End with a "Recommended Setup" section that synthesizes everything into a concrete recommendation for how I should set up my Claude Code environment and workflow for this project, given that I'm about to move from planning into POC work.
