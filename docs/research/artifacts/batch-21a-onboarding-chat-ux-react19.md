# Batch-21a — Guided Onboarding Chat UI for RunCoach Slice 1

**Research artifact** · `docs/research/artifacts/batch-21a-guided-onboarding-chat-ui.md`
**Scope:** Slice 1 (Onboarding → Plan) of MVP-0 on React 19 + Vite + TypeScript-strict + Tailwind + shadcn/ui + RTK Query + React Hook Form + Zod + React Router v7.
**Date:** April 24, 2026.
**Companion to:** DEC-0xx (guided-onboarding chat UI pattern choice).

---

## 0. TL;DR — Recommendation

**Build the Slice 1 guided onboarding chat on shadcn/ui primitives directly — do not adopt assistant-ui, CopilotKit, or any chat framework for this slice.** Compose `Form`, `RadioGroup`, `Input`, `Textarea`, `Button`, `ScrollArea`, `Avatar`, `Badge`, and `Progress` under React Hook Form + Zod, animate with `motion/react` (the renamed Framer Motion v12 that officially supports React 19) ([Motion](https://motion.dev/docs/react)), drive each turn through an RTK Query `mutation` that already carries the app's cookie+XSRF base-query, and render the assistant transcript inside a `role="log"` live region with an `aria-live="polite"` ([MDN `role=log`](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/log_role)).

**Why not assistant-ui.** assistant-ui is the leading React chat library in 2026, MIT-licensed, React 19–compatible, shadcn-styled ([assistant-ui npm](https://www.npmjs.com/package/@assistant-ui/react); [assistant-ui org](https://github.com/assistant-ui)), and would be the right call for Slice 4's open-conversation panel. But its entire architecture is built around a `Runtime` (LocalRuntime / ExternalStoreRuntime / DataStreamRuntime) and an `assistant-stream` generator API whose first-class contract is token-by-token streaming ([assistant-ui LocalRuntime docs](https://www.assistant-ui.com/docs/runtimes/custom/local); [assistant-ui data-stream docs](https://www.assistant-ui.com/docs/runtimes/data-stream)). For a deterministic-led, per-turn POST with structured metadata (`suggestedInputType`, `choices`, `nextExpectedTopic`, `needs_clarification`, `ready_for_plan`) that must render a **Zod-validated typed form field** rather than free text, the runtime is overhead we'd work around, and the message/thread abstractions fight React Hook Form. It's the right tool for Slice 4 and the wrong tool for Slice 1.

**Why not CopilotKit.** MIT-licensed ([CopilotKit GitHub](https://github.com/CopilotKit/CopilotKit)), but it is "not just a UI library — an agentic application framework" ([DEV, "I Evaluated Every AI Chat UI Library in 2026"](https://dev.to/alexander_lukashov/i-evaluated-every-ai-chat-ui-library-in-2026-heres-what-i-found-and-what-i-built-4p10)). It assumes AG-UI protocol / SSE, a CopilotKit runtime on the backend, and a streaming-first chat interaction model — none of which match a per-turn POST flow against a Wolverine/.NET backend using cookie auth + antiforgery. The lock-in category is "architecture/runtime," which the 2026 review explicitly flags as the heaviest.

**Slice 4 is a separate decision.** When Slice 4 lands (always-on right-rail chat, streaming, free-form, persisted history), re-evaluate assistant-ui as the primary candidate. The Slice 1 component should be written so its *message-bubble* and *transcript-scroller* primitives can be lifted into the Slice 4 container, but the turn-engine (deterministic, form-driven) is disposable — that's a feature, not a cost.

---

## 1. Library & Pattern Survey (Sub-question 1)

### 1.1 Candidate shortlist

| Candidate | 2026 status | License | React 19 | shadcn compat | Streaming-required | Fit for Slice 1 |
|---|---|---|---|---|---|---|
| **assistant-ui** | Most-popular 2026 React chat lib, YC-backed, `@assistant-ui/react` v0.12.25 (Apr 2026) ([npm](https://www.npmjs.com/package/@assistant-ui/react)) | MIT ([LICENSE](https://github.com/assistant-ui/assistant-ui/blob/main/LICENSE)) | Yes (React 18 & 19 documented) | Native — built on shadcn/ui + Radix primitives | Yes, runtime is stream-oriented (LocalRuntime yields `assistant-stream` chunks) | ❌ Architectural mismatch for per-turn POST with structured field output |
| **CopilotKit** | ~30k★, agentic framework, AG-UI protocol authors ([CopilotKit](https://github.com/CopilotKit/CopilotKit)) | MIT core; "commercial tiers available" for Cloud ([Pricing](https://www.copilotkit.ai/pricing)) | Yes | Optional; opinionated CopilotChat/CopilotPopup UI | Yes; SSE/AG-UI streaming assumption | ❌ Architecture lock-in; overshoots Slice 1 by 10x |
| **Vercel AI Elements / shadcn.io AI kit** | Production, Vercel AI SDK–coupled ([shadcn.io/ai](https://www.shadcn.io/ai)) | MIT | Yes | Yes | Yes, tightly coupled to `useChat` / AI SDK data stream | ❌ Designed for `message.parts` streaming model |
| **shadcn-chat (miskibin/chat-components, shadcn-chat.vercel.app)** | Active community blocks; copy-paste components ([shadcn-chat](https://shadcn-chat.vercel.app/); [miskibin/chat-components](https://github.com/miskibin/chat-components)) | MIT | Yes | Yes (they *are* shadcn) | No — pure presentational | ⚠️ Useful as a source of copy-paste bubble/scroller primitives, not as a framework |
| **Prompt Kit** | shadcn-ecosystem AI primitives collection ([shadcnstudio](https://shadcnstudio.com/blog/shadcn-chat-ui-example)) | MIT | Yes | Native | No | ⚠️ Same — copy-paste bits, not a framework |
| **TanStack AI** | Alpha, framework-agnostic hooks ([DEV review](https://dev.to/alexander_lukashov/i-evaluated-every-ai-chat-ui-library-in-2026-heres-what-i-found-and-what-i-built-4p10)) | MIT | Yes | Agnostic | No | ❌ Alpha, no production UIs |
| **Deep Chat** | Web Component, 3.3k★ ([DEV review](https://dev.to/alexander_lukashov/i-evaluated-every-ai-chat-ui-library-in-2026-heres-what-i-found-and-what-i-built-4p10)) | MIT | N/A (framework-agnostic WC) | No | Optional | ❌ Wrong framework model for a React 19 TS-strict SPA |
| **Stream Chat React / KendoReact / Syncfusion / CometChat** | Commercial SaaS-ish / enterprise chat SDKs | Mixed / commercial | Yes (Stream Chat ships with React 19 peer-dep caveats ([GetStream tutorial](https://getstream.io/chat/sdk/react/tutorial/))) | No | Feature-rich | ❌ SaaS/messaging focus, not guided AI onboarding |
| **Chainlit** | Python-first full-stack ([DEV review](https://dev.to/alexander_lukashov/i-evaluated-every-ai-chat-ui-library-in-2026-heres-what-i-found-and-what-i-built-4p10)) | MIT (maintainers changed May 2025) | N/A | No | Yes | ❌ Python-only; irrelevant |
| **Custom on shadcn/ui + motion/react** | — | — | Yes | Native | No | ✅ **Recommended for Slice 1** |

### 1.2 Why assistant-ui looks tempting but is wrong for Slice 1

assistant-ui's own docs frame it as "Typescript/React Library for AI Chat" and describe every runtime option — LocalRuntime, ExternalStoreRuntime, DataStreamRuntime — in terms of a `ChatModelAdapter` whose canonical method is `async *run({...})` yielding `assistant-stream` chunks ([LocalRuntime docs](https://www.assistant-ui.com/docs/runtimes/custom/local); [DataStreamRuntime docs](https://www.assistant-ui.com/docs/runtimes/data-stream)). The `ExternalStoreRuntime` can technically host non-streaming responses, but the accepted "snapshot-style yield" pattern in the maintainers' discussions is still generator-based ([Discussion #2123](https://github.com/assistant-ui/assistant-ui/discussions/2123)). For a flow where each turn must (a) validate the user's answer against a **per-slot Zod schema** (radio/date/number/range/text) inside React Hook Form, (b) POST with an idempotency key + XSRF header, and (c) swap input primitives on the next render based on `suggestedInputType`, we'd be fighting the runtime at every step. The Thread/Composer/Message primitives are also not designed for the "no free-form text box during guided slots" constraint — Slice 1's composer is **the currently-expected input widget**, not a universal textarea.

### 1.3 Why CopilotKit is wrong for Slice 1

CopilotKit is *the* pick if you want a "drop-in full-stack agentic copilot." Its premise is a CopilotKit runtime on the backend speaking AG-UI / SSE and a `<CopilotKit>` provider on the frontend with `useCopilotChat` / `<CopilotChat>` / `<CopilotPopup>` / headless-UI ([npm](https://www.npmjs.com/package/@copilotkit/react-ui); [CopilotKit product](https://www.copilotkit.ai/product)). RunCoach's Slice 1 backend is Wolverine/.NET with per-turn POSTs, antiforgery, and cookie auth — adopting CopilotKit means either shimming its runtime or bypassing most of the framework.

### 1.4 Evidence that "build it yourself on shadcn/ui" is viable and standard in 2026

- The shadcn/ui Form docs explicitly cover React Hook Form + Zod integration with `FormField` + `Controller` and discriminated-union-friendly patterns ([shadcn Form docs](https://ui.shadcn.com/docs/forms/react-hook-form)).
- shadcn-ecosystem chat blocks (shadcn-chat, Prompt Kit, shadcn.io blocks) already ship the cosmetic pieces — message bubble, scroller, toolbar, typing indicator — under Tailwind + motion/react with MIT licenses ([shadcn-chat](https://shadcn-chat.vercel.app/); [shadcnstudio](https://shadcnstudio.com/blog/shadcn-chat-ui-example); [shadcn.io/ai](https://www.shadcn.io/ai)).
- The 2026 "conversational onboarding" UX literature (Typeform, Duolingo, Airtable, Navattic, Loom) uses **one-question-at-a-time structured forms framed as chat**, not free-form chat widgets ([Eleken wizard UI](https://www.eleken.co/blog-posts/wizard-ui-pattern-explained); [Candu on Airtable onboarding](https://www.candu.ai/blog/airtables-best-wizard-onboarding-flow); [StartupSpells](https://startupspells.com/p/typeform-one-field-onboarding-ux-gas-snapchat-duolingo-spotify-signup-conversion); [UserGuiding](https://userguiding.com/blog/what-is-an-onboarding-wizard-with-examples)). Slice 1 is a wizard-with-conversational-paint, not a chat.

---

## 2. Dynamic input-affordance rendering from structured metadata (Sub-question 2, 11)

### 2.1 Chosen pattern: discriminated-union Zod schema + component map, wired through `useForm` re-initialized per turn

Every turn, the backend returns:

```ts
type TurnResponse = {
  reply: string;
  extracted: PartialAnswer | null;
  confidence: number;
  needs_clarification: boolean;
  nextExpectedTopic: Topic | null;
  suggestedInputType: "text" | "radio" | "date" | "number" | "range" | null;
  choices: string[] | null;
  ready_for_plan: boolean;
};
```

Slot-side, we model the answer as a Zod discriminated union keyed on `suggestedInputType`, which is the community-standard RHF + Zod dynamic-forms pattern in 2026 ([DEV: discriminatedUnion + RHF](https://dev.to/csar_zoleko_e6c3bb497f0d/dynamic-forms-with-discriminatedunion-and-react-hook-form-276a); [peturgeorgievv blog](https://peturgeorgievv.com/blog/complex-form-with-zod-nextjs-and-typescript-discriminated-union); [jossafossa/react-hook-form-example](https://github.com/jossafossa/react-hook-form-example)):

```ts
// src/features/onboarding/schemas.ts
import { z } from "zod";

export const AnswerSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("text"),
             value: z.string().trim().min(1, "Please answer.") }),
  z.object({ kind: z.literal("radio"),
             value: z.string().min(1, "Pick an option.") }),
  z.object({ kind: z.literal("date"),
             value: z.coerce.date() }),
  z.object({ kind: z.literal("number"),
             value: z.coerce.number().finite() }),
  z.object({ kind: z.literal("range"),
             value: z.coerce.number().min(0).max(10) }),
]);
export type Answer = z.infer<typeof AnswerSchema>;
```

Field rendering is a **typed component map** keyed on `suggestedInputType`. Each entry wraps a shadcn/ui primitive in an RHF `Controller` or `FormField`:

```tsx
const INPUT_RENDERERS: Record<NonNullable<TurnResponse["suggestedInputType"]>,
  React.ComponentType<FieldRendererProps>> = {
  text:   TextFieldRenderer,   // shadcn/ui Input
  radio:  RadioFieldRenderer,  // shadcn/ui RadioGroup + Controller
  date:   DateFieldRenderer,   // shadcn/ui Popover + Calendar (controlled)
  number: NumberFieldRenderer, // shadcn/ui Input type="number" + inputMode="decimal"
  range:  RangeFieldRenderer,  // shadcn/ui Slider
};
```

The shadcn docs explicitly endorse this `Controller`-wrapped pattern for `RadioGroup` + Zod `z.enum` ([shadcn Form docs](https://ui.shadcn.com/docs/forms/react-hook-form); [shadcn.io radio pattern](https://www.shadcn.io/patterns/form-advanced-4)), and 2026 tutorials confirm the stack ([shadcnstudio: RHF + Zod + shadcn](https://shadcnstudio.com/blog/react-hook-form-zod-shadcn-ui)).

### 2.2 Critical TypeScript-strict detail — one form per turn, fresh `useForm`

Because each turn may render a different input type, do **not** try to stretch a single `useForm<Answer>` across turns — under `discriminatedUnion`, the field names differ per branch and RHF's Zod resolver has known issues keeping unused-branch errors out of `formState.errors` ([colinhacks/zod #2180](https://github.com/colinhacks/zod/discussions/2180); [colinhacks/zod #2202](https://github.com/colinhacks/zod/issues/2202); [react-hook-form/resolvers #793](https://github.com/react-hook-form/resolvers/issues/793)).

Instead: **unmount the form on each turn** with a React `key={turnId}` on the `<OnboardingForm>` component. A fresh `useForm` per turn sidesteps the union-validation gotchas and makes `defaultValues` easy — the turn's answer is a single `value` of a single branch.

### 2.3 Persisted slots live outside RHF

Answers that have already landed — one per `Topic` — are kept in a local `slots: Record<Topic, PartialAnswer>` (component state or a tiny Redux slice). The RHF form is transient: it only owns the current turn's draft.

---

## 3. Variable-length progress indication (Sub-questions 3, 10)

### 3.1 The constraint

The topic list has a known minimum (`6` topics: PrimaryGoal, TargetEvent, CurrentFitness, WeeklySchedule, InjuryHistory, Preferences) but an unknown upper bound because `needs_clarification: true` adds an indeterminate tail. That rules out "Step 3 of 6" literal framing — it would mislead users when a clarification stretches the count.

### 3.2 Accessibility rule (WAI-ARIA)

Per W3C APG, a progressbar with an indeterminate portion **must omit `aria-valuenow` entirely** — the ARIA spec defines "no valuenow and no valuetext" as "indeterminate" ([W3C APG range properties](https://www.w3.org/WAI/ARIA/apg/practices/range-related-properties/); [MDN progressbar role](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/progressbar_role); [MDN aria-valuenow](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-valuenow); [FreedomScientific #353](https://github.com/FreedomScientific/standards-support/issues/353)). Simultaneously, a progressbar is required to have an accessible name (`aria-label` or `aria-labelledby`).

### 3.3 Recommended pattern: **segmented topic checklist + soft copy, no percentage**

Visually render six dots or pills, one per locked topic, ticking as each slot captures a non-null `extracted` with `confidence ≥ threshold`. When a clarification turn is in flight, the current dot shows an indeterminate shimmer; it does not advance. Copy reads **"About X of 6 — {{topicLabel}}"** while >1 topic remains, flipping to **"Final step"** on the last topic and **"Generating your plan…"** on `ready_for_plan`.

This matches 2026 UX consensus on onboarding progress trackers — segmented/checklist-style trackers outperform raw percentages for variable flows ([Mobbin progress indicators](https://mobbin.com/glossary/progress-indicator); [Medium: Stepper UI design](https://medium.com/@david.pham_1649/beyond-the-progress-bar-the-art-of-stepper-ui-design-cfa270a8e862); [UserGuiding progress trackers](https://userguiding.com/blog/progress-trackers-and-indicators); [Smart Interface Design Patterns](https://smart-interface-design-patterns.com/articles/onboarding-ux/)), and aligns with Airtable's explicit guidance to "use a progress bar and remove the number of steps" in onboarding wizards ([Candu](https://www.candu.ai/blog/airtables-best-wizard-onboarding-flow)).

### 3.4 Concrete accessibility markup

```tsx
<div
  role="progressbar"
  aria-label="Onboarding progress"
  aria-valuetext={progressLabel}           // e.g. "About 3 of 6. Current step: Target event."
  // intentionally no aria-valuenow / aria-valuemin / aria-valuemax
>
  <ol className="flex gap-2" aria-hidden="true">
    {TOPICS.map((t) => (
      <li key={t.id}
          data-state={slotState(t.id)}     // "complete" | "current" | "pending"
          className={…} />
    ))}
  </ol>
</div>
<p className="sr-only">{progressLabel}</p>
```

Notes:
- `aria-valuetext` carries the human-readable label screen readers speak, replacing the usual percent calculation ([W3C APG](https://www.w3.org/WAI/ARIA/apg/practices/range-related-properties/)).
- The dot list is `aria-hidden` — it's a visual redundancy; the label owns the semantics.
- On `needs_clarification: true`, `progressLabel` becomes *"Still on: {{topicLabel}}. We need a bit more info."* — the progress does not "tick" for a clarification, which matches users' mental model.

---

## 4. RTK Query integration (Sub-questions 4, 12)

### 4.1 Mutation is the right tool; don't try useQuery

Each turn is a non-idempotent POST that advances server-side state. That is the textbook RTK Query `mutation` case ([Redux Toolkit Mutations docs](https://redux-toolkit.js.org/rtk-query/usage/mutations)). The chat-history-on-screen concern is a **UI concern, not a cache concern** — the history is appended to local component state from each mutation's resolved response.

```ts
// src/features/onboarding/api.ts
export const onboardingApi = createApi({
  reducerPath: "onboardingApi",
  baseQuery: baseQueryWithXsrf, // from @/app/baseQuery, see §4.3
  endpoints: (build) => ({
    postOnboardingTurn: build.mutation<TurnResponse, TurnRequest>({
      query: ({ idempotencyKey, ...body }) => ({
        url: "/api/v1/onboarding/turn",
        method: "POST",
        body,
        headers: { "Idempotency-Key": idempotencyKey },
      }),
    }),
    // Slice 1 does NOT expose a getOnboardingHistory query —
    // the transcript lives in component state for this slice.
  }),
});
```

### 4.2 Why no `useQuery` for the transcript in Slice 1

A `getOnboardingHistory` query with `invalidatesTags` on each mutation would work but buys nothing: (a) the transcript never exists outside the onboarding session, (b) on unmount we navigate to the plan and the component disposes, (c) round-tripping to rebuild state we already have doubles latency. RTK Query's own docs recommend invalidation only when "considering your cached data as a reflection of the server-side state" ([RTK Query Manual Cache Updates](https://redux-toolkit.js.org/rtk-query/usage/manual-cache-updates)) — the onboarding transcript isn't that.

### 4.3 Base-query: cookie + XSRF (and the Laravel/Rails pattern applied to .NET)

Per ASP.NET Core's guidance for SPAs, the client should read the `XSRF-TOKEN` cookie and echo it as `X-XSRF-TOKEN` on state-changing requests ([Microsoft Learn: antiforgery for SPAs](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery); [OWASP CSRF cheatsheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)). Because RunCoach uses `__Host-` cookies, `credentials: "include"` is required on every fetch.

```ts
// src/app/baseQuery.ts
import { fetchBaseQuery } from "@reduxjs/toolkit/query/react";

const XSRF_COOKIE = "__Host-XSRF-TOKEN";
const XSRF_HEADER = "X-XSRF-TOKEN";

function readCookie(name: string): string | undefined {
  return document.cookie
    .split("; ")
    .find((c) => c.startsWith(name + "="))
    ?.split("=")[1];
}

export const baseQueryWithXsrf = fetchBaseQuery({
  baseUrl: "/",
  credentials: "include",          // __Host- cookies
  prepareHeaders: (headers, { type }) => {
    if (type === "mutation") {
      const token = readCookie(XSRF_COOKIE);
      if (token) headers.set(XSRF_HEADER, decodeURIComponent(token));
    }
    return headers;
  },
});
```

This mirrors the long-standing Laravel-Sanctum / Angular `withXsrfConfiguration` / Axios `xsrfCookieName` pattern applied to RTK Query via `prepareHeaders` ([reduxjs/redux-toolkit #3034](https://github.com/reduxjs/redux-toolkit/discussions/3034); [OWASP cheatsheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)).

### 4.4 Idempotency key and double-submit prevention

Generate a UUIDv4 per turn-submit *before* dispatch and send it as `Idempotency-Key` — this is the 2026 industry-standard pattern, driven by Stripe's convention and the IETF draft ([HTTPToolkit: Idempotency Keys RFC](https://httptoolkit.com/blog/idempotency-keys/); [Emmanuella Okorie](https://www.emmanuellaokorie.com/blog/why-your-api-needs-idempotency-keys); [Medium: Stripe-like idempotency](https://greenmonkii.medium.com/implementing-idempotency-in-backend-systems-270657c546cb); [DEV 2026](https://dev.to/young_gao/designing-idempotency-apis-why-your-post-endpoint-needs-to-handle-duplicates-4o3n)). The key must **persist across retries** of the same user action; only regenerate when the user edits the answer. Combine with UI disable:

```tsx
const [submitTurn, { isLoading, error }] = usePostOnboardingTurnMutation();
// Submit button: disabled while isLoading OR while form is submitting.
// On failure, user can retry — we re-send the same idempotency key.
```

### 4.5 Optimistic vs pessimistic — recommendation: **pessimistic**

RTK Query supports both ([RTK Query Manual Cache Updates](https://redux-toolkit.js.org/rtk-query/usage/manual-cache-updates)), but for onboarding:

- The user message **can** be appended optimistically to the transcript on submit — it's the user's literal text, nothing to speculate.
- The **assistant reply must be pessimistic** — we don't know what it will say (it may be `needs_clarification` asking for more detail). Showing a placeholder "typing…" indicator that swaps in the real reply on `onQueryStarted` resolution is the pattern.
- On 4xx/5xx: keep the user bubble, replace "typing…" with an inline `role="alert"` (polite, inline — not assertive; screen readers still announce) + a Retry button that re-submits with the same idempotency key.

### 4.6 Error taxonomy

| Status | UI | User action |
|---|---|---|
| 200 | Append assistant reply; transition form to next turn | — |
| 400 / 422 validation | Inline error under the current input; stay on turn | Edit answer, resubmit |
| 401/403 | Redirect to login (baseQuery-level `fetchBaseQueryWithReauth` wrapper) | Sign in again |
| 409 idempotency-in-flight | Show "Still processing your previous answer…" | Wait; do not resubmit |
| 5xx / network | Inline "Something went wrong" card + Retry button | Retry (same key) |

---

## 5. Accessibility — async assistant turns (Sub-question 5)

### 5.1 Transcript: `role="log"` (implicit `aria-live="polite"`, `aria-atomic="false"`)

The ARIA `log` role is the purpose-built match for a sequential chat transcript where new items arrive at the end and old items may disappear — "examples include chat logs, messaging history, game log, or an error log" ([MDN log role](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/log_role); [W3C ARIA23 technique](https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA23); [DigitalA11Y](https://www.digitala11y.com/log-role/)). It has implicit `aria-live="polite"` and implicit `aria-atomic="false"`, so only new messages get announced, not the entire history — which is what we want.

```tsx
<ol role="log" aria-label="Onboarding conversation" aria-relevant="additions">
  {transcript.map((m) => <MessageBubble key={m.id} {...m} />)}
</ol>
```

`role="feed"` is the wrong choice here — `feed` is for scrollable article streams with focusable articles and load-more semantics ([DigitalA11Y role=feed](https://www.digitala11y.com/feed-role/)), closer to a social timeline than a bounded onboarding conversation.

### 5.2 Focus management on each turn

1. User submits → focus stays on the submit button until the mutation resolves.
2. On success, **move focus to the new input affordance** (`radio` first radio, `text` input, etc.) — use `useEffect` with a ref that fires on `turnId` change. This respects WCAG 2.2 SC 2.4.3 Focus Order and 2.4.11 Focus Not Obscured ([W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/); [Deque WCAG 2.2](https://dequeuniversity.com/resources/wcag-2.2/); [AllAccessible 2.4.11 guide](https://www.allaccessible.org/blog/wcag-2411-focus-not-obscured-minimum-implementation-guide)).
3. The assistant message's arrival is announced by the `role="log"` container — do **not** additionally move focus into the assistant bubble. Doing both creates a double announcement ([A11Y Collective: aria-live](https://www.a11y-collective.com/blog/aria-live/); [Sara Soueidan on live regions](https://www.sarasoueidan.com/blog/accessible-notifications-with-aria-live-regions-part-1/)).
4. On `needs_clarification`, the reply is still a regular assistant message — the log region handles it.
5. On submit error, render an inline `role="alert"` near the input; don't move focus (the user already has focus nearby).

### 5.3 Why `polite`, not `assertive`

2025–2026 live-region guidance is unanimous: use polite for the default, reserve assertive for time-critical errors ([MDN aria-live](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-live); [Sara Soueidan](https://www.sarasoueidan.com/blog/accessible-notifications-with-aria-live-regions-part-1/); [Right Said James, Aug 2025](https://rightsaidjames.com/2025/08/aria-live-regions-when-to-use-polite-assertive/); [Tarnoff, Sep 2025](https://tarnoff.info/2025/09/29/quick-tip-aria-live-regions/); [MDN log role — polite by default](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Techniques/Using_the_log_role)). Assistant replies aren't urgent enough to interrupt.

### 5.4 WCAG 2.2 checklist hit by this design

- **2.4.3 Focus Order** — deterministic move to the new input on each turn.
- **2.4.11 Focus Not Obscured (Min, AA)** — the submit button and input must not be hidden under iOS keyboard or sticky header.
- **2.4.13 Focus Appearance (AAA)** — shadcn default focus ring satisfies 3:1 contrast with `outline-offset`.
- **2.5.8 Target Size (Min, AA)** — chat input and submit button ≥ 24×24 CSS px (shadcn Button default passes).
- **3.2.6 Consistent Help** — out of scope for onboarding itself.
- **3.3.1 Error Identification / 3.3.3 Error Suggestion** — inline Zod errors via `FormMessage`.
- **4.1.3 Status Messages** — delivered by `role="log"` and `role="alert"`.

### 5.5 Mobile screen readers

iOS VoiceOver and Android TalkBack both honor `role="log"` and `aria-live="polite"` ([A11Y Collective live regions](https://www.a11y-collective.com/blog/aria-live/)). Two caveats: avoid combining `role="alert"` + `aria-live="assertive"` on iOS — it double-announces ([UXPin](https://www.uxpin.com/studio/blog/aria-live-regions-for-dynamic-content/)); and test with rotor navigation on iOS to confirm the log container gets a labeled landmark.

---

## 6. Animation and latency affordances (Sub-question 6)

### 6.1 Per-turn latency is ~1–5 s (Sonnet 4.5 call)

That's **too long for a silent interface** but too short for a full skeleton. The affordance layer:

1. **On user submit** → user bubble appears (local append, instant), followed by an **assistant typing indicator** (3-dot bouncing, fixed width so the bubble doesn't resize on fill).
2. **On response** → typing indicator is replaced by the real assistant text (fade + slide up 4px).
3. **Progress indicator** stays stable during the call (no false-advance shimmer on the pending topic).

### 6.2 Library choice: `motion/react` (the new name for Framer Motion)

Framer Motion v12 officially supports React 19 and has been **renamed to Motion** with a new import path `motion/react` ([Motion react docs](https://motion.dev/docs/react); [Motion component docs](https://motion.dev/docs/react-motion-component); [npm framer-motion](https://www.npmjs.com/package/framer-motion); [Motion upgrade guide](https://motion.dev/docs/react-upgrade-guide)). MIT licensed, 30M+ monthly downloads. React 19 compatibility issues in older Framer Motion versions are well documented ([motiondivision/motion #2668](https://github.com/motiondivision/motion/issues/2668); [nandorojo/moti #383](https://github.com/nandorojo/moti/issues/383)), but v12 (`motion@12+`) is clean.

Slice 1 uses motion/react for three things only:
- `<AnimatePresence>` wrapping each new message bubble so entry/exit is smooth.
- `layout` prop on the bubbles so list height auto-adjusts without jank.
- A tiny `motion.span` for the typing-indicator dots.

Everything else is Tailwind + CSS transitions.

### 6.3 `prefers-reduced-motion` is mandatory

Per MDN and WCAG 2.3.3 (AAA but widely adopted), animations must collapse to static or near-static alternatives when `prefers-reduced-motion: reduce` is set ([MDN prefers-reduced-motion](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@media/prefers-reduced-motion); [Pope Tech](https://blog.pope.tech/2025/12/08/design-accessible-animation-and-movement/); [CSS-Tricks](https://css-tricks.com/almanac/rules/m/media/prefers-reduced-motion/)). Concrete application:

- Typing indicator: swap the bouncing dots for static text **"Coach is typing…"** (a pulsing dot communicates status; a static label is the static alternative per Pope Tech).
- Message entry: `AnimatePresence` + `initial={false}` + zero-duration variants under reduced motion.
- No parallax, no slide-from-far.

```tsx
const prefersReducedMotion = useReducedMotion(); // from motion/react
const messageVariants = prefersReducedMotion
  ? { initial: {}, animate: {}, exit: {} }
  : { initial: { opacity: 0, y: 4 }, animate: { opacity: 1, y: 0 }, exit: { opacity: 0 } };
```

---

## 7. Slice 1 vs Slice 4 differentiation (Sub-question 7)

### 7.1 The architectural seam

| Axis | **Slice 1 (this)** | **Slice 4 (future)** |
|---|---|---|
| Layout | Full-page, single-column | Always-visible right rail (desktop) / bottom drawer (mobile) |
| Transport | Per-turn POST | SSE / streaming |
| Input | Constrained to `suggestedInputType` | Free-form textarea + send |
| History | In-memory this session | Persisted, paginated |
| End-state | `ready_for_plan: true` → route change | Open-ended |
| Framework | **Custom on shadcn/ui + RHF + Zod** | **assistant-ui recommended** (re-evaluate) |

### 7.2 Product precedent for this split

Linear's onboarding is a full-page multi-step wizard distinct from its in-app comment threads. Vercel's setup flows use progress-tracker wizards ([Userpilot](https://userpilot.com/blog/onboarding-wizard/); [Eleken](https://www.eleken.co/blog-posts/wizard-ui-pattern-explained)). AI consoles (Anthropic, OpenAI) similarly split "getting started" wizards from their conversation UIs. The AI-UX literature is explicit that **guided vs open are different tools**: "Don't replace forms — evolve them. Conversational agents should complement, not cannibalize, efficient data capture." ([Medium: Traditional Forms vs Conversational Interactions](https://medium.com/design-bootcamp/agentic-ux-in-enterprise-when-to-use-conversational-agents-vs-traditional-forms-93cf588eac21)); "Conversational UI vs chatbot UI: A chatbot typically follows scripted flows with predefined options, while conversational UI accepts free-form natural language. Choose based on whether your use case needs flexibility (conversational) or reliability (scripted)" ([AI UX Design Guide](https://www.aiuxdesign.guide/patterns/conversational-ui)).

### 7.3 Shared primitives to extract now

To minimize Slice 4 rework, keep these in a **shared** module (`src/components/chat/*`), not `src/features/onboarding/*`:

- `<MessageBubble role="user" | "assistant">` — pure presentational, Tailwind classes, no state.
- `<MessageGroup>` — wraps consecutive same-role bubbles, handles avatar stacking.
- `<TranscriptScroll>` — scroll container with `role="log"`, auto-scroll-to-bottom behavior (can be swapped for assistant-ui's `Thread` scroller in Slice 4).
- `<TypingIndicator>` — dots + reduced-motion fallback.
- `useAutoScrollOnNewMessage()` — hook that scrolls to bottom when a message appears and the user isn't scrolled up.

**Slice-1-only** (`src/features/onboarding/*`):

- Dynamic input renderers (`TextFieldRenderer`, `RadioFieldRenderer`, …).
- Turn-engine (`useOnboardingTurn` hook wrapping the RTK Query mutation + slot state).
- Progress indicator.

When Slice 4 adopts assistant-ui, the `MessageBubble`/`TranscriptScroll` styling transfers via assistant-ui's primitive-composition model; the turn-engine is thrown away; no regrets.

---

## 8. Testing pattern (Sub-question 8)

### 8.1 Vitest + React Testing Library — unit test one turn end-to-end

Mock the RTK Query mutation with MSW (preferred) or `setupApiStore` from `@reduxjs/toolkit/query/react`. Drive the form with `userEvent` and assert transcript growth with `findByRole`:

```ts
test("renders next question after user submits a radio answer", async () => {
  server.use(
    http.post("/api/v1/onboarding/turn", () =>
      HttpResponse.json({
        reply: "Great. What's your target event?",
        extracted: { topic: "PrimaryGoal", value: "race-prep" },
        confidence: 0.9,
        needs_clarification: false,
        nextExpectedTopic: "TargetEvent",
        suggestedInputType: "radio",
        choices: ["5k", "10k", "half", "full"],
        ready_for_plan: false,
      } satisfies TurnResponse)),
  );

  render(<Onboarding />, { wrapper: TestProviders });

  const user = userEvent.setup();
  await user.click(screen.getByRole("radio", { name: /race prep/i }));
  await user.click(screen.getByRole("button", { name: /continue/i }));

  // Find the new assistant bubble once the mutation resolves.
  const nextQuestion = await screen.findByText(/what's your target event/i);
  expect(nextQuestion).toBeInTheDocument();

  // The new input affordance is now 5k/10k/half/full radios.
  expect(await screen.findByRole("radio", { name: /5k/i })).toBeInTheDocument();
});
```

Pitfalls to avoid (documented in RTL community): `findBy*` is the correct async query — `getBy*` throws before the new element paints; wrapping `getByRole` in `waitFor` works too but `findByRole` is the idiom ([Testing Library ByRole docs](https://testing-library.com/docs/queries/byrole/); [Sheelah Brennan: RTL tips](https://sheelahb.com/blog/react-testing-library-tips-and-tricks/); [Tim Deschryver: correct query](https://timdeschryver.dev/blog/making-sure-youre-using-the-correct-query)). `happy-dom` has had intermittent issues with `findByRole` in late 2024 ([happy-dom #1302](https://github.com/capricorn86/happy-dom/issues/1302)); prefer **jsdom** for this slice.

### 8.2 Playwright — the full transcript loop

Chain `page.waitForResponse` with the action that triggers it, matching on URL and status — this is the officially-documented pattern and the 2026 consensus ([Playwright Page docs](https://playwright.dev/docs/api/class-page); [Playwright Network docs](https://playwright.dev/docs/network); [BrowserStack 2026](https://www.browserstack.com/guide/playwright-waitforresponse); [Checkly guide](https://www.checklyhq.com/blog/monitoring-responses-in-playwright/); [DEV: Playwright Quirks](https://dev.to/rmarinsky/playwright-quirks-waitforresponse-21p6)):

```ts
test("completes the deterministic 6-topic flow", async ({ page }) => {
  await page.goto("/onboarding");

  for (const topic of TOPICS) {
    await expect(page.getByRole("log")).toContainText(topic.expectedQuestion);

    // Start waiting BEFORE the action. Never `await` the promise here.
    const resp = page.waitForResponse((r) =>
      r.url().includes("/api/v1/onboarding/turn") && r.status() === 200);

    await answerTurn(page, topic);
    const r = await resp;
    const body = await r.json();
    expect(body.ready_for_plan).toBe(topic.id === "Preferences");
  }

  await expect(page).toHaveURL("/home"); // ready_for_plan routed us out
});
```

Key pitfalls (2026): the most common `waitForResponse` failure is declaring the wait **after** the action, which races to timeout ([BrowserStack](https://www.browserstack.com/guide/playwright-waitforresponse); [Playwright Quirks](https://dev.to/rmarinsky/playwright-quirks-waitforresponse-21p6)). Prefer specific predicates over wildcards — we have one endpoint so URL match is fine.

### 8.3 Testing `needs_clarification`

Unit test: queue MSW responses in sequence — first returns `needs_clarification: true` with the same topic, second returns next topic. Assert the topic dot did **not** advance and that the input re-rendered focused. This is the one test that catches an easy-to-miss regression.

### 8.4 Testing accessibility

- `@axe-core/playwright` run against the onboarding page at each turn.
- Unit assertions on `role="log"`, `role="progressbar"` with `aria-valuetext` (no `aria-valuenow`), and that the submit button becomes `aria-busy="true"` while the mutation is in flight.

---

## 9. Bundle, compliance, licensing (Sub-question 9)

### 9.1 Bundle estimate for the recommended custom approach

All figures **gzipped, tree-shaken** on a Vite production build; figures are directional not binding.

| Dependency | Purpose | Gzipped add (approx) | Notes |
|---|---|---|---|
| React 19 + React DOM | Runtime | ~45 KB | Already in app |
| React Router v7 | Routing | ~12 KB | Already in app |
| @reduxjs/toolkit + RTK Query | State/API | ~15 KB | Already in app |
| react-hook-form | Forms | ~9 KB | Already in app |
| zod + @hookform/resolvers | Validation | ~12 KB | Already in app |
| **Onboarding-only additions:** | | | |
| motion/react (v12) | Bubble + typing animations | ~18 KB | Used app-wide eventually; Slice 1 pays first ([Motion docs](https://motion.dev/docs/react)) |
| Radix primitives used by shadcn Form/RadioGroup/Popover/Calendar | shadcn internals | ~15–20 KB incremental | Already partly present via shadcn; shared across app ([Radix UI npm](https://www.npmjs.com/package/radix-ui)) |
| date-fns (only if DateFieldRenderer needed) | Date formatting | ~8 KB tree-shaken | Avoidable if we render a plain `<input type="date">` |
| uuid (for idempotency keys) | Crypto UUID | ~1 KB, or 0 KB using `crypto.randomUUID()` | Use native; drop the dep |
| **Slice-1 delta estimate** | | **~18–25 KB gzipped net** | Motion is the dominant cost |

For comparison, assistant-ui's `@assistant-ui/react` core (0.12.25, Apr 2026) depends on Radix + shadcn-style primitives ([npm](https://www.npmjs.com/package/@assistant-ui/react)) and typically adds 30–50 KB gzipped on top of Radix for its runtime/primitives, plus `@assistant-ui/react-markdown` or `@assistant-ui/react-streamdown` for rendering (more if streaming/Shiki). Exact deltas should be measured per app, and "library size favors libraries with less functionality" ([Thoughtspile: bundle-size lies](https://thoughtspile.github.io/2022/02/15/bundle-size-lies/)) — but the order of magnitude confirms the custom approach is smaller *and* only costs what we actually use.

### 9.2 Compliance

- **React 19**: No `findDOMNode`, no UNSAFE lifecycles — `motion/react` v12 ≥ 12.0.0 handles the React 19 `ref` prop change ([Motion component docs](https://motion.dev/docs/react-motion-component)); shadcn/ui's Button note about wrapping in `forwardRef` for React 18 compat does **not** apply on React 19 ([assistant-ui compatibility note](https://www.assistant-ui.com/llms-full.txt), same rule applies to any shadcn block).
- **TypeScript strict + `noUncheckedIndexedAccess`**: discriminated-union `AnswerSchema` guarantees branch inference; `choices[i]` accesses should be narrowed with an explicit non-null check or `?? ""` before display.
- **No CSS-in-JS runtime**: motion/react uses inline style / `transform` — no Emotion, no styled-components. Clean with Tailwind.
- **No MUI**: none of the recommended packages transitively pull `@mui/*`.
- **Licensing**: React (MIT), Radix (MIT), shadcn (MIT, copy-paste), RHF (MIT), Zod (MIT), motion (MIT), RTK (MIT). All compatible with RunCoach's Apache-2.0 code and CC-BY-NC-SA-4.0 content split.

### 9.3 Linting

`eslint-plugin-sonarjs` / SonarAnalyzer rule sets around cognitive complexity and hook-dep correctness apply normally; no special rules needed for this slice. RHF's `Controller` pattern with `fieldState` is the 2026 idiom and lints cleanly ([shadcn Form docs](https://ui.shadcn.com/docs/forms/react-hook-form)).

---

## 10. Edit-prior-answer UX (Sub-question 13)

### 10.1 Recommended pattern: inline review chips above the composer, expandable per topic

Per `ReviseAnswer(Topic, NewValue)`, the UI needs to let a user edit a captured slot **without restarting**. Slice 1 uses a persistent "Your answers so far" chip row above the input area:

```
[ Primary goal: Race prep  ✎ ]   [ Target event: 10k  ✎ ]   [ Current fitness: …  ✎ ]
```

Clicking a chip opens an inline edit **for that topic only** — the chat-turn input swaps to that topic's `suggestedInputType` with the prior value pre-filled, and submit posts a `ReviseAnswer` turn. The conversational transcript records "Updated *Target event*: 10k → Half marathon" as a system bubble (distinguishable styling, `role="status"`-adjacent but rendered inline inside the `role="log"`).

This is preferable to:
- **Sidebar review list** — redundant with the chip row and steals horizontal space on mobile.
- **Separate settings flow** — the product is conversational; breaking flow to edit contradicts the whole premise.
- **"Back" button** — implies a linear stack; a revise turn may target any slot.

### 10.2 No library ships this out of the box

assistant-ui's `MessageActions` supports per-message edit, but that's "edit the user's previous utterance," not "edit a captured structured slot." The chip-row pattern is custom but trivial to implement: a `<RevisionChip topic={t} value={slots[t]} onEdit={…} />` component and a branch in the turn-engine to tag the next submit as `ReviseAnswer`.

### 10.3 Accessibility

Chip row is a `role="toolbar"` labeled "Captured answers." Each chip is a `button` whose accessible name includes both topic label and current value ("Edit target event, currently 10k"). This satisfies WCAG 2.5.8 target size and avoids relying on the pencil icon alone.

---

## 11. Plan-generation handoff (Sub-question 14)

### 11.1 Pattern: inline "Generating your plan…" state, then route change

On `ready_for_plan: true`:

1. Replace the onboarding composer area with a **plan-generation card** — avatar + "Generating your plan…" + animated skeleton (reduced-motion: static label).
2. Fire a short-interval poll or listen on the existing session channel (out of scope for Slice 1 — backend-side off Wolverine event subscription). For MVP-0 Slice 1, a simple polled GET of a `/plan/status` endpoint is acceptable fallback.
3. When the plan resolves, `navigate("/home", { replace: true })` via React Router v7 so Back doesn't re-enter onboarding. Unmount the onboarding component.

Do **not** keep the onboarding component mounted behind the plan view. The onboarding session is over; its RTK Query state can be invalidated. Re-entering onboarding (if the user somehow navigates back) should require explicit "Restart onboarding" action.

### 11.2 Precedent

Duolingo, Airtable, Navattic, Loom all use a final celebratory / summary state before transitioning to the product home ([UserGuiding wizards](https://userguiding.com/blog/what-is-an-onboarding-wizard-with-examples); [Candu Airtable walkthrough](https://www.candu.ai/blog/airtables-best-wizard-onboarding-flow); [Smart Interface Design Patterns](https://smart-interface-design-patterns.com/articles/onboarding-ux/)). The "generating" state **is** that celebratory state for RunCoach — it reinforces that their data is being used, and bridges the latency.

---

## 12. Mobile-responsive behavior (Sub-question 15)

### 12.1 Layout primitive

```tsx
<div className="flex h-dvh flex-col">   {/* dynamic viewport height */}
  <header className="shrink-0 pt-[env(safe-area-inset-top)]">…</header>
  <main role="log" className="flex-1 overflow-y-auto px-4">…</main>
  <footer className="shrink-0 pb-[env(safe-area-inset-bottom)]">
    {/* composer */}
  </footer>
</div>
```

Key decisions:

- **`h-dvh`** (Tailwind's dynamic viewport unit) — the 2025 consensus replacement for `100vh` on mobile. When the iOS/Android keyboard opens, `dvh` shrinks to the visible viewport so the composer stays visible ([DEV: Fix mobile keyboard overlap with dvh](https://dev.to/franciscomoretti/fix-mobile-keyboard-overlap-with-visualviewport-3a4a)).
- **`env(safe-area-inset-*)`** — respects iPhone notch/Dynamic Island and home indicator ([codestudy.net iOS keyboard](https://www.codestudy.net/blog/how-to-make-fixed-content-go-above-ios-keyboard/); [bram.us VirtualKeyboard](https://www.bram.us/2021/09/13/prevent-items-from-being-hidden-underneath-the-virtual-keyboard-by-means-of-the-virtualkeyboard-api/)).
- **`<meta name="viewport" content="width=device-width, initial-scale=1, interactive-widget=resizes-content">`** — newer property that tells Chrome Android to resize the visual viewport so fixed-bottom content stays visible ([bramus viewport-resize-behavior explainer](https://github.com/bramus/viewport-resize-behavior/blob/main/explainer.md)). iOS Safari doesn't honor this yet; `dvh` is the real fix there.
- **Composer placement** — thumb-reachable (bottom 1/3 of screen), with submit on the right for right-handed-dominant ergonomics. Submit button is ≥ 44×44 CSS px (Apple HIG) / 48×48 (Android) — exceeds WCAG 2.5.8 (24×24).
- **`overscroll-behavior: contain`** on the transcript to prevent iOS bounce-scroll pulling body content up behind the composer.

### 12.2 Focus-visible on iOS

When the virtual keyboard opens after focus moves to the new input, we must ensure the input isn't scrolled *under* the sticky header (WCAG 2.4.11 Focus Not Obscured). CSS `scroll-padding-top` on `html` + `scroll-margin-top` on focusable elements is the portable fix ([AllAccessible 2.4.11](https://www.allaccessible.org/blog/wcag-2411-focus-not-obscured-minimum-implementation-guide)):

```css
html { scroll-padding-top: 80px; }
:where(input, textarea, button):focus { scroll-margin-top: 96px; }
```

### 12.3 No library opinion wins mobile

assistant-ui, shadcn-chat, Prompt Kit all default to desktop-first layouts; mobile-specific keyboard handling is on us regardless of choice.

---

## 13. Component-shape sketch (the concrete deliverable)

```tsx
// src/features/onboarding/OnboardingScreen.tsx
"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { usePostOnboardingTurnMutation } from "./api";
import { AnswerSchema, type Answer } from "./schemas";
import { INPUT_RENDERERS } from "./fields";
import {
  MessageBubble, TranscriptScroll, TypingIndicator,
} from "@/components/chat";
import { ProgressDots } from "./ProgressDots";
import { RevisionChipRow } from "./RevisionChipRow";
import { Button } from "@/components/ui/button";
import { Form } from "@/components/ui/form";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";

export function OnboardingScreen() {
  const navigate = useNavigate();
  const [turn, setTurn] = useState<TurnResponse>(INITIAL_TURN);
  const [transcript, setTranscript] = useState<Message[]>([
    { id: "assistant-0", role: "assistant", text: INITIAL_TURN.reply },
  ]);
  const [slots, setSlots] = useState<Record<Topic, PartialAnswer>>({});
  const [submitTurn, { isLoading, error }] = usePostOnboardingTurnMutation();
  const idempotencyKeyRef = useRef<string>(crypto.randomUUID());
  const prefersReducedMotion = useReducedMotion();

  // Fresh form per turn — keyed by turn id in the parent.
  const form = useForm<Answer>({
    resolver: zodResolver(AnswerSchema),
    defaultValues: defaultForInputType(turn.suggestedInputType),
    mode: "onSubmit",
  });

  // Move focus to the first input when the turn changes.
  const inputMountRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    inputMountRef.current?.querySelector<HTMLElement>(
      'input,button[role="radio"],textarea'
    )?.focus();
  }, [turn]);

  async function onSubmit(values: Answer) {
    const userMessage: Message = {
      id: `user-${Date.now()}`,
      role: "user",
      text: presentAnswer(values, turn),
    };
    setTranscript((t) => [...t, userMessage]);
    try {
      const next = await submitTurn({
        idempotencyKey: idempotencyKeyRef.current,
        answer: values,
        turnContext: { expectedTopic: turn.nextExpectedTopic },
      }).unwrap();

      setTranscript((t) => [
        ...t,
        { id: `assistant-${Date.now()}`, role: "assistant", text: next.reply },
      ]);
      if (next.extracted && !next.needs_clarification) {
        setSlots((s) => ({ ...s, [next.extracted!.topic]: next.extracted!.value }));
      }
      if (next.ready_for_plan) {
        navigate("/home", { replace: true });
        return;
      }
      setTurn(next);
      idempotencyKeyRef.current = crypto.randomUUID(); // only rotate on success
    } catch {
      // Keep idempotencyKey stable so Retry is safe.
    }
  }

  const InputRenderer = turn.suggestedInputType
    ? INPUT_RENDERERS[turn.suggestedInputType]
    : null;

  const progressLabel = computeProgressLabel(slots, turn);

  return (
    <div className="flex h-dvh flex-col">
      <header className="shrink-0 pt-[env(safe-area-inset-top)] px-4 py-3 border-b">
        <div
          role="progressbar"
          aria-label="Onboarding progress"
          aria-valuetext={progressLabel}
        >
          <ProgressDots slots={slots} currentTopic={turn.nextExpectedTopic} />
          <p className="sr-only">{progressLabel}</p>
        </div>
        <RevisionChipRow slots={slots} onEdit={(topic) => beginRevision(topic)} />
      </header>

      <TranscriptScroll>
        <AnimatePresence initial={false}>
          {transcript.map((m) => (
            <motion.div
              key={m.id}
              layout={!prefersReducedMotion}
              initial={prefersReducedMotion ? false : { opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
            >
              <MessageBubble role={m.role}>{m.text}</MessageBubble>
            </motion.div>
          ))}
          {isLoading && (
            <motion.div key="typing" initial={false}>
              <TypingIndicator />
            </motion.div>
          )}
        </AnimatePresence>
        {error && (
          <div role="alert" className="text-sm text-destructive">
            Something went wrong. <Button variant="link" onClick={() => form.handleSubmit(onSubmit)()}>Retry</Button>
          </div>
        )}
      </TranscriptScroll>

      <footer className="shrink-0 border-t px-4 py-3 pb-[env(safe-area-inset-bottom)]">
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex gap-2 items-end">
            <div ref={inputMountRef} className="flex-1">
              {InputRenderer && (
                <InputRenderer
                  control={form.control}
                  choices={turn.choices ?? undefined}
                />
              )}
            </div>
            <Button type="submit" disabled={isLoading} aria-busy={isLoading}>
              {isLoading ? "Sending…" : "Continue"}
            </Button>
          </form>
        </Form>
      </footer>
    </div>
  );
}
```

Notes on this sketch:
- The `<OnboardingScreen>` component is wrapped by a parent that forces remount via `key={turn.id}` on turn boundary so RHF starts fresh (addresses §2.2).
- `presentAnswer(values, turn)` maps `{ kind: "radio", value: "half" }` → `"Half marathon"` using the current turn's choice list.
- The typing indicator lives inside the same `AnimatePresence` so reduced-motion is respected automatically.
- `aria-busy="true"` on the submit button is the 2026 RTL-queryable pattern ([Testing Library ByRole](https://testing-library.com/docs/queries/byrole/)).

---

## 14. Differentiation note — what to name and where to put things

| File/path | Slice 1 | Survives into Slice 4 |
|---|---|---|
| `src/components/chat/MessageBubble.tsx` | ✅ used | ✅ reused (style tokens shared) |
| `src/components/chat/TranscriptScroll.tsx` | ✅ used | ⚠️ replaced by assistant-ui `Thread` (Slice 4 decision) |
| `src/components/chat/TypingIndicator.tsx` | ✅ used | ✅ reused |
| `src/components/chat/useAutoScrollOnNewMessage.ts` | ✅ used | ⚠️ assistant-ui handles this internally; likely retire |
| `src/features/onboarding/*` | ✅ the whole slice | ❌ intentionally disposable — onboarding ends |
| `src/features/onboarding/api.ts` | ✅ `postOnboardingTurn` mutation | — (different endpoint for Slice 4) |
| `src/features/onboarding/fields/*` | ✅ `TextFieldRenderer`, … | — (Slice 4 is free-form textarea) |

The explicit deal: the **cosmetic chat primitives are shared**, the **turn-engine and field renderers are onboarding-only**. This way Slice 4 can adopt assistant-ui without ripping out cosmetic components; and Slice 1's logic doesn't warp under pressure to generalize prematurely.

---

## 15. Summary capability matrix

| Axis | **Custom on shadcn/ui** (recommended) | assistant-ui | CopilotKit | Vercel AI Elements | shadcn-chat blocks |
|---|---|---|---|---|---|
| React 19 + TS-strict | ✅ full control | ✅ ([npm](https://www.npmjs.com/package/@assistant-ui/react)) | ✅ | ✅ | ✅ |
| shadcn/ui compatibility | ✅ native | ✅ native | ⚠️ opinionated UI | ✅ | ✅ native |
| Per-turn POST (non-streaming) fit | ✅ ideal | ⚠️ runtime is stream-first | ❌ SSE/AG-UI | ❌ streaming-first | ✅ presentational only |
| RTK Query composability | ✅ direct | ⚠️ runtime state competes | ❌ own state layer | ⚠️ own hooks | ✅ agnostic |
| Dynamic input affordances (RHF+Zod) | ✅ discriminated union | ⚠️ not the model | ⚠️ Generative UI different abstraction | ⚠️ | ✅ |
| Bundle (Slice 1 delta) | **~18–25 KB gz** | +30–50 KB gz core + extras | +50–80 KB gz + runtime | +20–40 KB gz | ~0 (copy-paste) |
| a11y (ARIA) | ✅ own it, role="log" + polite | ✅ ([assistant-ui marketing](https://www.assistant-ui.com/)) | ✅ | ✅ | ⚠️ varies by block |
| License | MIT chain | MIT | MIT (Cloud commercial) | MIT | MIT |
| Maintenance signal | N/A (our code) | Very active, v0.12.25 Apr 2026 | Very active ([CopilotKit blog](https://www.copilotkit.ai/blog/easily-build-a-ui-for-your-ai-agent-in-minutes-langgraph-copilotkit)) | Very active | Variable |
| MUI/CSS-in-JS leakage | ❌ none | ❌ none | ❌ none | ❌ none | ❌ none |
| Slice 4 reuse | Partial (primitives) | ✅ full reuse candidate | ✅ if we pivot to AG-UI | ✅ if we adopt Vercel stack | Partial |

---

## 16. Final recommendation, explicit

**Land for Slice 1:** Custom guided-chat UI on shadcn/ui + RHF + Zod + RTK Query + motion/react, following the component sketch in §13, the progress pattern in §3, the a11y wiring in §5, the mobile layout in §12, and the testing approach in §8.

**Reject for Slice 1, revisit for Slice 4:** assistant-ui.

**Reject outright for this product shape:** CopilotKit (architecture lock-in to AG-UI); Vercel AI Elements (streaming-first coupling to AI SDK); commercial chat SDKs (wrong problem).

**Hard constraints this design satisfies:**
1. Per-turn POST with structured metadata, no streaming — ✅ RTK Query mutation.
2. Cookie + XSRF — ✅ `prepareHeaders` in base query.
3. TypeScript strict + `noUncheckedIndexedAccess` — ✅ discriminated-union answer schema; explicit narrowing on `choices` access.
4. React 19 clean — ✅ motion/react v12, shadcn/ui React 19 patterns.
5. WCAG 2.2 AA — ✅ `role="log"` + polite, `aria-valuetext` on indeterminate progress, focus management, reduced-motion.
6. Bundle-aware — ✅ ~18–25 KB gz Slice 1 delta, smaller than the nearest library option.
7. Slice 4 forward-compatible — ✅ cosmetic primitives separated; turn-engine disposable.

---

## Citations (primary sources)

### Libraries and 2026 landscape
- Alexander Lukashov, "I Evaluated Every AI Chat UI Library in 2026. Here's What I Found (and What I Built)", DEV, March 2026 — https://dev.to/alexander_lukashov/i-evaluated-every-ai-chat-ui-library-in-2026-heres-what-i-found-and-what-i-built-4p10
- assistant-ui npm package — https://www.npmjs.com/package/@assistant-ui/react
- assistant-ui GitHub organization (last commit Apr 24, 2026) — https://github.com/assistant-ui
- assistant-ui LICENSE (MIT) — https://github.com/assistant-ui/assistant-ui/blob/main/LICENSE
- assistant-ui LocalRuntime docs — https://www.assistant-ui.com/docs/runtimes/custom/local
- assistant-ui Pick-a-Runtime — https://www.assistant-ui.com/docs/runtimes/pick-a-runtime
- assistant-ui Data Stream Runtime — https://www.assistant-ui.com/docs/runtimes/data-stream
- assistant-ui ExternalStoreRuntime — https://www.assistant-ui.com/docs/runtimes/custom/external-store
- assistant-ui React compatibility — https://www.assistant-ui.com/llms-full.txt
- CopilotKit GitHub — https://github.com/CopilotKit/CopilotKit
- CopilotKit Product page (MIT open source core) — https://www.copilotkit.ai/product
- CopilotKit react-ui npm — https://www.npmjs.com/package/@copilotkit/react-ui
- Vercel AI Elements — https://www.shadcn.io/ai, https://www.shadcn.io/ai/chatbot
- shadcn-chat community block — https://shadcn-chat.vercel.app/
- miskibin/chat-components — https://github.com/miskibin/chat-components
- Prompt Kit & shadcn chat UI examples — https://shadcnstudio.com/blog/shadcn-chat-ui-example
- Radix UI Primitives — https://www.radix-ui.com/primitives/docs/overview/introduction; https://www.npmjs.com/package/radix-ui

### React Hook Form + Zod + shadcn
- shadcn/ui React Hook Form docs — https://ui.shadcn.com/docs/forms/react-hook-form
- shadcn RadioGroup form pattern — https://www.shadcn.io/patterns/form-advanced-4; https://www.shadcn.io/patterns/radio-group-form-1
- Dynamic forms with discriminatedUnion + RHF — https://dev.to/csar_zoleko_e6c3bb497f0d/dynamic-forms-with-discriminatedunion-and-react-hook-form-276a
- Complex forms with Zod discriminated union — https://peturgeorgievv.com/blog/complex-form-with-zod-nextjs-and-typescript-discriminated-union
- Advanced RHF patterns — https://github.com/jossafossa/react-hook-form-example
- Zod resolver discriminated-union quirks — https://github.com/colinhacks/zod/discussions/2180; https://github.com/colinhacks/zod/issues/2202; https://github.com/react-hook-form/resolvers/issues/793
- Master form handling with RHF + Zod + shadcn — https://shadcnstudio.com/blog/react-hook-form-zod-shadcn-ui

### RTK Query + auth
- Redux Toolkit RTK Query Mutations — https://redux-toolkit.js.org/rtk-query/usage/mutations
- RTK Query Manual Cache Updates — https://redux-toolkit.js.org/rtk-query/usage/manual-cache-updates
- Redux Essentials Part 8: RTK Query Advanced Patterns — https://redux.js.org/tutorials/essentials/part-8-rtk-query-advanced
- RTK Query XSRF discussion — https://github.com/reduxjs/redux-toolkit/discussions/3034
- Microsoft Learn antiforgery for SPAs — https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery
- OWASP CSRF Prevention Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html

### Idempotency
- Why Your API Needs Idempotency Keys — https://www.emmanuellaokorie.com/blog/why-your-api-needs-idempotency-keys
- Designing Idempotent APIs (DEV 2026) — https://dev.to/young_gao/designing-idempotent-apis-why-your-post-endpoint-needs-to-handle-duplicates-4o3n
- Stripe-like Idempotency Keys in Postgres — https://brandur.org/idempotency-keys
- Implementing Idempotency in Backend Systems — https://greenmonkii.medium.com/implementing-idempotency-in-backend-systems-270657c546cb
- Working with the new Idempotency Keys RFC — https://httptoolkit.com/blog/idempotency-keys/

### Accessibility (WCAG 2.2, ARIA APG, live regions)
- W3C WCAG 2.2 — https://www.w3.org/TR/WCAG22/
- W3C What's New in WCAG 2.2 — https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/
- Deque WCAG 2.2 Updates — https://dequeuniversity.com/resources/wcag-2.2/
- AllAccessible WCAG 2.2 complete guide — https://www.allaccessible.org/blog/wcag-22-complete-guide-2025
- AllAccessible Focus Not Obscured (2.4.11) — https://www.allaccessible.org/blog/wcag-2411-focus-not-obscured-minimum-implementation-guide
- TPGi managing focus guidance — https://vispero.com/resources/managing-focus-and-visible-focus-indicators-practical-accessibility-guidance-for-the-web/
- MDN ARIA live regions — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions; https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-live
- MDN ARIA log role — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/log_role; https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Techniques/Using_the_log_role
- W3C ARIA23: Using role=log — https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA23
- DigitalA11Y role=log — https://www.digitala11y.com/log-role/
- DigitalA11Y role=feed — https://www.digitala11y.com/feed-role/
- Sara Soueidan on live regions — https://www.sarasoueidan.com/blog/accessible-notifications-with-aria-live-regions-part-1/
- A11Y Collective: aria-live complete guide — https://www.a11y-collective.com/blog/aria-live/
- A11Y Collective: aria-alert — https://www.a11y-collective.com/blog/aria-alert/
- Right Said James: aria-live cheatsheet (Aug 2025) — https://rightsaidjames.com/2025/08/aria-live-regions-when-to-use-polite-assertive/
- Nat Tarnoff: ARIA live regions (Sep 2025) — https://tarnoff.info/2025/09/29/quick-tip-aria-live-regions/
- UXPin ARIA live regions — https://www.uxpin.com/studio/blog/aria-live-regions-for-dynamic-content/
- Universal Design Ireland live regions — https://universaldesign.ie/communications-digital/web-and-mobile-accessibility/web-accessibility-techniques/developers-introduction-and-index/use-aria-appropriately/use-aria-to-announce-updates-and-messaging

### Progress bars (indeterminate)
- W3C APG Range Properties — https://www.w3.org/WAI/ARIA/apg/practices/range-related-properties/
- MDN progressbar role — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/progressbar_role
- MDN aria-valuenow — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-valuenow
- W3C APG Meter pattern — https://www.w3.org/WAI/ARIA/apg/patterns/meter/
- Adobe React Aria ProgressBar — https://react-spectrum.adobe.com/react-aria/useProgressBar.html
- Indeterminate progress bar JAWS behavior — https://github.com/FreedomScientific/standards-support/issues/353

### Onboarding UX precedent
- Eleken: Wizard UI pattern — https://www.eleken.co/blog-posts/wizard-ui-pattern-explained
- Andrew Coyle: How to design a form wizard — https://www.andrewcoyle.com/blog/how-to-design-a-form-wizard
- Candu: Airtable onboarding wizard teardown — https://www.candu.ai/blog/airtables-best-wizard-onboarding-flow
- UserGuiding: Onboarding wizard examples (2026) — https://userguiding.com/blog/what-is-an-onboarding-wizard-with-examples
- UserGuiding: Progress trackers and indicators — https://userguiding.com/blog/progress-trackers-and-indicators
- Userpilot: Onboarding wizard critique — https://userpilot.com/blog/onboarding-wizard/
- Mobbin: Progress indicator UI design — https://mobbin.com/glossary/progress-indicator
- Smart Interface Design Patterns: Onboarding UX — https://smart-interface-design-patterns.com/articles/onboarding-ux/
- Medium: Beyond the progress bar — Stepper UI (Feb 2026) — https://medium.com/@david.pham_1649/beyond-the-progress-bar-the-art-of-stepper-ui-design-cfa270a8e862
- StartupSpells: Typeform one-field onboarding UX — https://startupspells.com/p/typeform-one-field-onboarding-ux-gas-snapchat-duolingo-spotify-signup-conversion
- Bootcamp Medium: Traditional forms vs conversational interactions — https://medium.com/design-bootcamp/agentic-ux-in-enterprise-when-to-use-conversational-agents-vs-traditional-forms-93cf588eac21
- AI UX Design Guide: Conversational UI — https://www.aiuxdesign.guide/patterns/conversational-ui
- Onething: Conversational UI best practices — https://www.onething.design/post/best-practices-for-conversational-ui-design
- GetStream: Chat UX best practices — https://getstream.io/blog/chat-ux/

### Animation & reduced motion
- Motion for React docs — https://motion.dev/docs/react
- Motion component docs — https://motion.dev/docs/react-motion-component
- Motion upgrade guide — https://motion.dev/docs/react-upgrade-guide
- npm framer-motion — https://www.npmjs.com/package/framer-motion
- motion React 19 compatibility issue (resolved in v12) — https://github.com/motiondivision/motion/issues/2668
- Moti/framer-motion React 19 — https://github.com/nandorojo/moti/issues/383
- MDN prefers-reduced-motion — https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@media/prefers-reduced-motion
- Pope Tech: Design accessible animation (Dec 2025) — https://blog.pope.tech/2025/12/08/design-accessible-animation-and-movement/
- CSS-Tricks: prefers-reduced-motion — https://css-tricks.com/almanac/rules/m/media/prefers-reduced-motion/
- cssShowcase: prefers-reduced-motion — https://www.cssshowcase.com/articles/animation/prefers-reduced-motion
- Microsoft Edge DevTools reduced-motion simulation — https://learn.microsoft.com/en-us/microsoft-edge/devtools-guide-chromium/accessibility/reduced-motion-simulation

### Mobile / viewport
- DEV: Fix mobile keyboard overlap with dvh — https://dev.to/franciscomoretti/fix-mobile-keyboard-overlap-with-visualviewport-3a4a
- codestudy.net: Fixed content above iOS keyboard — https://www.codestudy.net/blog/how-to-make-fixed-content-go-above-ios-keyboard/
- bram.us: VirtualKeyboard API — https://www.bram.us/2021/09/13/prevent-items-from-being-hidden-underneath-the-virtual-keyboard-by-means-of-the-virtualkeyboard-api/
- bramus viewport-resize-behavior explainer — https://github.com/bramus/viewport-resize-behavior/blob/main/explainer.md
- mattpilott/ios-chat solution — https://github.com/mattpilott/ios-chat
- Medium: Fixing Safari mobile resizing bug — https://medium.com/@krutilin.sergey.ks/fixing-the-safari-mobile-resizing-bug-a-developers-guide-6568f933cde0

### Testing
- Playwright Page API — https://playwright.dev/docs/api/class-page
- Playwright Network docs — https://playwright.dev/docs/network
- BrowserStack 2026: Playwright waitForResponse — https://www.browserstack.com/guide/playwright-waitforresponse
- Checkly: Monitoring responses in end-to-end tests — https://www.checklyhq.com/blog/monitoring-responses-in-playwright/
- DEV: Playwright Quirks — waitForResponse — https://dev.to/rmarinsky/playwright-quirks-waitforresponse-21p6
- DZone: Playwright real-time apps — https://dzone.com/articles/playwright-for-real-time-applications-testing-webs
- Testing Library ByRole — https://testing-library.com/docs/queries/byrole/
- Tim Deschryver: making sure you're using the correct query — https://timdeschryver.dev/blog/making-sure-youre-using-the-correct-query
- Sheelah Brennan: RTL tips and tricks — https://sheelahb.com/blog/react-testing-library-tips-and-tricks/
- happy-dom findByRole issue — https://github.com/capricorn86/happy-dom/issues/1302