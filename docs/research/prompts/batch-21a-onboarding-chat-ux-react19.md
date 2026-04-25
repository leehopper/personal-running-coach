# Research Prompt: Batch 21a — R-065

# Guided Multi-Turn Onboarding Chat UI Pattern — React 19 + shadcn/ui + RTK Query (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19 + TypeScript-strict + Vite SPA on the established RunCoach frontend stack (Tailwind + shadcn/ui + RTK Query + React Hook Form + Zod + React Router v7), what is the current 2026 best-practice pattern for building a **guided, deterministic-led, multi-turn onboarding chat UI** that consumes a per-turn structured-output API — and which library/pattern choice should land in Slice 1?

## Context

I'm preparing Slice 1 (Onboarding → Plan) of MVP-0 for RunCoach, an AI running coach. The whole product is conversational: Slice 1 is guided chat-driven onboarding, Slice 3 surfaces proactive adaptation messages in a read-only chat panel, Slice 4 turns that panel into an always-on interactive chat. **This prompt is scoped to Slice 1's guided onboarding only.** Slice 4's open-conversation UI is explicitly a separate concern.

The onboarding flow (locked by R-048 / DEC-047 — `docs/research/artifacts/batch-16a-onboarding-conversation-state.md`):

- **Deterministic-led, LLM-phrased.** A static topic list (`PrimaryGoal`, `TargetEvent`, `CurrentFitness`, `WeeklySchedule`, `InjuryHistory`, `Preferences`) controls slot order; the LLM phrases questions, handles follow-ups, and extracts structured answers per turn.
- **Per-turn request/response, not streaming.** Each turn is one POST to `/api/v1/onboarding/turn` with the user's answer + idempotency key; backend returns the LLM's next question plus structured metadata. No SSE / WebSocket / streaming response — that lands in Slice 4.
- **Variable-length flow.** The topic list is ~6 items, but the LLM may request clarification (`needs_clarification: true`) turns at any slot, so total turn count is `6 + N_clarifications`. Progress indication must handle this without promising exact step counts.
- **Per-turn structured metadata** the frontend needs to render. Working shape (subject to spec): `{ reply: string; extracted: PartialAnswer | null; confidence: number; needs_clarification: bool; nextExpectedTopic: Topic | null; suggestedInputType: "text" | "radio" | "date" | "number" | "range" | null; choices: string[] | null; ready_for_plan: bool }`. The frontend renders the reply as assistant text and renders the appropriate input affordance (text field / radio / date picker / number / range slider) based on `suggestedInputType` + `choices`.
- **Completion triggers plan generation.** When `ready_for_plan: true` lands, the frontend navigates to the home surface (Slice 1's plan view) — the actual plan-generation LLM call happens server-side off a Wolverine event subscription, not synchronously in the onboarding turn request.
- **Cookie session + antiforgery.** Slice 0 landed `__Host-` cookies + `[RequireAntiforgeryToken]`. Every onboarding POST must carry the XSRF header — RTK Query base-query must handle this.

### Frontend stack pins (already locked)

- **React 19** (Vite SPA, no SSR, no server components in play).
- **TypeScript strict** (`"strict": true` + `"noUncheckedIndexedAccess": true`).
- **Tailwind + shadcn/ui** for primitives.
- **RTK Query** for API state.
- **React Hook Form + Zod** for form validation.
- **React Router v7** for navigation.
- **Vitest + React Testing Library** for component tests; **Playwright** for E2E.
- No streaming, no SSE, no WebSocket in Slice 1.

### What the existing research covers — and doesn't

- `batch-10a-frontend-latest-practices.md` establishes React 19 + Vite + Tailwind + shadcn/ui + RTK Query conventions at the framework level. It does **not** prescribe a chat-flow pattern, a progress-indicator idiom for variable-length flows, or a library for dynamic per-turn input affordances.
- `batch-4a-coaching-conversation-design.md` covers coaching tone, OARS/GROW patterns, question ordering — content/voice concerns, not UI mechanics.
- No artifact addresses the "guided chat with structured-output-driven input affordances" pattern specifically.
- The R-053 eval pattern (`batch-17c-multi-turn-llm-eval-pattern.md`) evaluates the backend multi-turn flow; it's orthogonal to UI choice but the chosen UI must be Playwright-drivable per scenario.

## Research Question

**Primary:** What is the current 2026 best-practice pattern — library choice, component shape, accessibility idioms, RTK Query integration, progress-indication pattern — for a guided multi-turn onboarding chat UI on a React 19 + shadcn/ui + TypeScript-strict SPA that consumes a per-turn structured-output API and renders dynamic input affordances? Which specific library or pattern should land in Slice 1?

**Sub-questions (must be actionable):**

1. **Library / pattern survey.** For each candidate, document 2026 status, license, React 19 + TS-strict compliance, shadcn/ui compatibility, RTK Query composability, bundle-size impact (gzip, per-route), accessibility quality (ARIA conformance, keyboard navigation, screen-reader behavior), TypeScript-strict story, and active-maintenance signal. **Candidates to compare at minimum:**
   - **`assistant-ui`** (shadcn-style headless chat components, Radix-based)
   - **`@copilotkit/react-ui`** (opinionated chat primitives + headless hooks)
   - **`shadcn-chat`** or equivalent community shadcn-ecosystem chat block
   - **Custom-built on shadcn/ui primitives** (`Form`, `Input`, `RadioGroup`, `Button`, `ScrollArea`, `Avatar`, `Badge`) + Framer Motion / `motion/react` for animation
   - **Any 2026 React-ecosystem chat libraries** I haven't named — survey the landscape honestly
   - Explicitly exclude SaaS widgets (Intercom/Drift/etc.) and libraries built around streaming-only assumptions that don't work for non-streaming per-turn POSTs.

2. **Dynamic input-affordance rendering from structured metadata.** Given the per-turn shape `{ suggestedInputType, choices, nextExpectedTopic, ... }`, what's the 2026 pattern for rendering the appropriate input primitive? React Hook Form + Zod schema-per-input-type with a discriminated-union pattern, a headless `field-renderer` library, or component-map per `suggestedInputType`? What does the leading candidate library do here, and does it compose with React Hook Form + Zod or fight it?

3. **Variable-length progress indication.** The flow has a deterministic topic list (known `minExpectedTurns`) but variable clarification turns (unknown upper bound). What's the pattern — segmented progress bar with "estimated" framing, percentage with a ceiling soft-cap, stepped dots with an indeterminate tail, copy-first ("we're almost done") without a numeric indicator? Accessibility implication (`aria-valuenow` on an indeterminate progress). What do the leading chat-onboarding patterns in 2026 actually ship?

4. **RTK Query integration — per-turn mutation pattern.** Onboarding turns are non-idempotent POSTs (each advances state). RTK Query's `useMutation` is the obvious fit, but the chat UI needs the prior turns rendered on screen (message history). Options: (a) `useMutation` per turn + client-side message log in component state; (b) `useQuery` for full history after each mutation + `invalidatesTags`; (c) RTK Query cache with optimistic updates. What's the current 2026 consensus? How does this interact with the established `__Host-` cookie + XSRF antiforgery header RTK Query base-query shape (Slice 0)?

5. **Accessibility — async assistant turns.** The assistant response arrives asynchronously after the user submits. Best-practice idioms: `aria-live="polite"` region for incoming assistant text, focus-management on the new input affordance, keyboard-only flow, screen-reader announcement of the `needs_clarification` path, focus-trap for modal-style onboarding vs. full-page. WCAG 2.2 / ARIA APG 2026 guidance. Mobile-viewport considerations (iOS VoiceOver, Android TalkBack).

6. **Animation and latency affordances.** The per-turn POST takes ~1-5 s (Sonnet 4.5 call). Standard loading affordances: typing indicator, skeleton message, dot-flash, progress shimmer. Framer Motion / `motion/react` vs CSS-only. What does the leading library ship, and does it respect `prefers-reduced-motion`?

7. **Differentiation from the Slice 4 open-conversation UI.** The Slice 4 chat panel is always visible (right rail on desktop, bottom drawer on mobile), streaming-capable, free-form, with a history persisted across sessions. Slice 1 onboarding is full-page, guided, non-streaming, with a defined end-state. How do other products (Linear onboarding, Vercel wizard, Cursor intake, Anthropic console setup, any AI-coach onboarding flows) differentiate guided-chat from open-chat visually and architecturally? Shared primitives, divergent containers, or fully separate components? Pattern recommendation.

8. **Testing pattern — Vitest + RTL + Playwright.** How do you test a multi-turn chat UI? Unit: render → mock each turn's mutation response → assert message stack grows. E2E: Playwright drives `getByRole("textbox")` + `keyboard.press("Enter")` loop. What are the pitfalls (async message rendering + RTL's `findByRole`, Playwright's `waitFor(response)` vs. `waitFor(locator)`)? What do 2026 testing-library / Playwright conventions prescribe for this shape?

9. **Bundle and compliance.** React 19 support (no `findDOMNode`, no UNSAFE lifecycles), TypeScript strict compliance in 2026, `eslint-plugin-sonarjs` / SonarAnalyzer equivalent signal, license compatibility with Apache-2.0 + CC-BY-NC-SA-4.0 (RunCoach's split per `batch-14g`). Explicit flag if any candidate pulls in MUI, Emotion, or other CSS-in-JS runtimes that conflict with Tailwind + shadcn/ui.

10. **Progress-indicator accessibility-plus-copy pattern.** Because the LLM's clarification turns are unbounded, the "3 of 6" framing is wrong. Acceptable variants: "3 of about 6", "Almost done", "Final step", a segmented checklist that ticks as each topic completes, an estimated-time indicator. Which pattern tests best with screen readers and respects `prefers-reduced-motion`? Any 2026 conversational-UI writeups on this specifically?

11. **Structured-metadata → form-field rendering strategy.** Concrete: when `suggestedInputType: "radio"` + `choices: ["5k", "10k", "half", "full"]` arrives, what's the 2026 idiomatic render path? shadcn/ui `RadioGroup` + React Hook Form `Controller` + Zod enum, or a dedicated field-renderer library, or the chat library's own typed input system? What's the cleanest pattern that keeps TypeScript strict discriminated-union happy?

12. **Form submission UX.** Per-turn submit triggers the POST; UI must prevent double-submit, handle 4xx (validation) vs 5xx (server) vs network failures, and keep the XSRF cookie/header flow correct. What's the current RTK Query + React Hook Form error-boundary pattern for a chat-per-turn mutation?

13. **`ReviseAnswer` / edit-prior-answer UX.** DEC-047 allows editing a prior captured answer via `ReviseAnswer(Topic, NewValue)` without restarting onboarding. What's the 2026 UX pattern — a chip per answered topic with an "edit" affordance that reopens that turn inline? A sidebar review list? A separate settings flow? How does the leading library (if any) support this?

14. **Plan-generation handoff.** When `ready_for_plan: true` lands, the frontend should surface "Generating your plan…" briefly and then navigate to the plan view. Is this a route change, a full-screen takeover, or an inline reveal? Should the onboarding component remain mounted (the generation happens server-side off a Wolverine event) or unmount on the `ready_for_plan` boundary? Pattern precedent in 2026 onboarding UIs.

15. **Mobile-first responsive behavior.** Onboarding must work on mobile (primary target for a personal running-coach product). Chat-container sizing, virtual keyboard behavior (iOS viewport shrink), safe-area-inset handling for notched devices, thumb-reachable submit button placement. Any library opinions on mobile vs. desktop chat containers?

## Why It Matters

- **The product is a chat product.** Slice 1's onboarding is the first impression and the first genuine "talk to your AI coach" moment. A janky pattern here — misaligned messages, broken focus, confusing progress, double-submits — undermines every later interaction. Getting this right once beats refactoring after Slice 4 ships a second chat component.
- **Guided vs. open is a real architectural seam.** If we choose a library that assumes free-form streaming chat, we'll fight it through Slice 1 and then find it was the wrong fit for the Slice 4 panel anyway. If we build the right shared primitives now, Slice 4 costs less.
- **Accessibility is load-bearing, not optional.** The product surface is mostly text; a chat UI without proper ARIA-live regions and focus management is functionally unusable with a screen reader. We should not ship that on v1.
- **TypeScript-strict + React 19 compliance is non-negotiable.** Slice 0 proved the stack is strict-clean; a library that forces `any` or non-strict-compatible types is an automatic no.
- **Bundle size matters for mobile.** RunCoach's primary user is on a phone. Adding 100 KB gzipped for a chat library we only use in onboarding is a tax worth measuring.

## Deliverables

- **A concrete recommendation** — one pattern/library choice with explicit rationale and the alternatives rejected.
- **A capability matrix** across the candidates on the axes in sub-question 1 (React 19 + TS-strict compliance, shadcn/ui compat, RTK Query compat, bundle size, a11y quality, licence, maintenance signal).
- **A component-shape sketch** — concrete React 19 + TS-strict + shadcn/ui component composition for one onboarding turn, showing:
  - Message history container with ARIA-live region
  - Assistant message bubble rendering
  - Dynamic input-affordance render based on `suggestedInputType` + `choices`
  - Submit + loading state + error state
  - Progress indication element with its accessibility attributes
- **A wiring sketch** — RTK Query endpoint definition for `postOnboardingTurn`, base-query cookie+XSRF configuration, optimistic-update pattern if applicable, error handling.
- **A progress-indicator pattern** — concrete component + copy + ARIA attribute choices for the variable-length case.
- **An edit-prior-answer UX recommendation** with component shape.
- **A mobile-responsive pattern** — chat container sizing, keyboard-avoidance, safe-area handling.
- **A testing sketch** — Vitest + RTL for unit; Playwright selector + wait-for pattern for E2E.
- **A differentiation note** — how the Slice 1 component relates to the future Slice 4 open-conversation panel; which primitives are shared, which containers diverge, to minimize future rework.
- **Citations** — current library docs (2026 versions), WCAG 2.2 / ARIA APG references, 2025-2026 React 19 chat-UI case studies or open-source examples, Anthropic / OpenAI / Linear onboarding-flow precedent.
- **A bundle-size estimate** for the recommended choice with each primary dependency called out.

## Out of Scope

- **Slice 4 open-conversation chat panel** — always-visible, streaming, free-form — researched separately when Slice 4 lands.
- **Plan-rendering UI** (this-week card + macro/meso/micro rendering) — Slice 1 scope but a separate design concern, not covered here.
- **LLM prompt content for onboarding** (`onboarding-v1.yaml`) — prompt engineering, not UI research; handled at spec time.
- **Backend multi-turn mechanics** — covered by R-048 / DEC-047 and R-053.
- **Auth / registration UI** — landed in Slice 0 (PR #63).
- **Streaming response rendering** — not in Slice 1; defer to Slice 4's prompt.
- **Conversation history persistence across sessions** — not in Slice 1 (onboarding is one-shot); Slice 4 concern.
- **Voice input / mid-run UI** — explicitly out of MVP-0 per cycle plan.
- **Internationalization / localization** — English-only for MVP-0.
- **Coach-personality theming / avatar design** — aesthetic/design pass, not research.
