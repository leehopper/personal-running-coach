# Vision & Principles

## One-Liner

An AI-powered running coach that builds, maintains, and continuously optimizes a personalized training plan through natural conversation and real-time adaptation.

## The Problem

Existing training tools fall into two camps: static plan generators that slot you into a template and never adjust, or expensive human coaches. Power users can hack together a coaching workflow using AI chat tools manually, but this requires significant prompt engineering skill and constant context management. There is no product that provides a persistent, adaptive AI coaching relationship out of the box.

## The Differentiator

This platform is not a plan generator. It is an AI orchestrator that maintains an ongoing coaching relationship. The plan is one output of that relationship. The real product is the persistent, adaptive intelligence that understands the user's body, goals, constraints, and training history — and reasons about all of it continuously.

## Core Principle

The system should do the heavy lifting that the user currently does manually when working with AI tools: maintaining context, re-prompting with history, managing plan state, and triggering the right kind of adjustment at the right time.

## Design Principles

Guiding principles to reference when making design decisions throughout development.

1. **The AI is the coach, not the user.** The system should manage complexity so the user doesn't have to. If the user needs to understand prompt engineering to get good results, the product has failed.

2. **Adapt to the human, not the other way around.** The plan serves the runner. If reality diverges from the plan, the plan changes — the runner doesn't get scolded.

3. **Tiered intelligence, not brute force.** Not every interaction needs a full AI reasoning cycle. Simple acknowledgments, minor adjustments, and routine logging should be lightweight. Save the heavy reasoning for meaningful decisions.

4. **Structured flexibility.** The system should be opinionated about training science (periodization, progressive overload, recovery) while remaining flexible about how life actually works.

5. **Design for handoff.** Every document, data structure, and design decision should be structured cleanly enough that an AI agent (in Claude Code or otherwise) can pick it up and build from it without re-explanation.

6. **Design for pivots.** Assume the product direction, architecture, and feature set will change many times. Favor modular, loosely coupled designs that can be rearranged without rewriting everything. Avoid deep commitments to specific implementations until they've been validated. The cost of changing course should always be low.

## Scope of This Project

This collection of planning documents is the "why and what" — not a technical specification. The following are intentionally deferred until POC work and PRD stage:

- Tech stack decisions
- Database schema or API design
- Specific prompt engineering
- UI mockups or wireframes
- Deployment architecture
