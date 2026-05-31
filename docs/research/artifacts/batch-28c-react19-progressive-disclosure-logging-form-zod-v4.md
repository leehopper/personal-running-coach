> **Research artifact — Batch 28c · R-079.** Commissioned via the RunCoach research protocol; prompt at `docs/research/prompts/batch-28c-react19-progressive-disclosure-logging-form-zod-v4.md`. Deep-web-research output landed & integrated 2026-05-31 (queue → Integrated). Feeds the Slice 2b frontend spec; reusable conventions locked as **DEC-075**. NOTE: the research agent's original H1 mislabeled this "R-076"; corrected to R-079 below. Verbatim research output follows.

---

# R-079 — Workout-Logging Form & Heterogeneous History List: Canonical 2026 Frontend Patterns

**Status:** Proposed · **Date:** 2026-05-31 · **Slice:** 2b (Workout Logging) · **Stack:** React 19.2.6, Vite 8, RTK Query (@reduxjs/toolkit 2.12), react-hook-form 7.76.1, zod 4.4.3, shadcn/ui (new-york), Radix unified 1.4.3, Tailwind v4.2, Orval 8.12.1 · **Path:** `docs/research/artifacts/`

## TL;DR

- **Stay on RHF + `zodResolver` + shadcn `Form`.** React 19 Actions add nothing for a client-side SPA form with conditional sections and a derived Zod schema — they ship no client-side validation, and there is no server action to integrate with under RTK Query. Use one `useForm` with the optional-metric section wrapped in shadcn `Collapsible` and `shouldUnregister: false` so filled-then-collapsed fields still submit.
- **The empty-numeric coercion footgun is the single highest-risk item (🔴).** `z.coerce.number().optional()` silently coerces `""→0` (Number(`""`)===0); RHF `valueAsNumber` yields `NaN`, which fails `z.number().optional()`. The canonical fix is a per-field `setValueAs: (v) => v === "" ? undefined : Number(v)` (or a schema-level `z.preprocess` blank→undefined step) paired with `z.number().optional()`, with the form schema derived from the Orval-generated request schema via `.pick()/.extend()`.
- **Render history with a `<dl>` showing only present metrics**, labels from a single shared `metric-meta` map; make **splits display-only in MVP-0** (no `useFieldArray`); and use **pessimistic RTK Query** (await success → invalidate the `History` LIST tag → refetch), consistent with the idempotency-key "try again" contract.

## Key Findings

### 🔴 Critical
1. **Empty optional numerics must resolve to `undefined`, not `NaN`/`0`.** Get this wrong and a minimum-payload save (distance + duration + status + notes, all metrics blank) throws validation errors and blocks the core path. `z.coerce.number()` is explicitly unsafe for optional fields: per colinhacks/zod Issue #2461, "empty strings are getting converted to a 0… if the field is required, validation passes because the schema sees a 0 which gives a false positive." (Coercion uses JS `Number()`, and `Number("") === 0`.)
2. **`shouldUnregister` must be `false`** (the RHF v7 default) so a metric typed then collapsed still submits. The RHF `useForm` docs: "By default, an input value will be retained when input is removed. However, you can set `shouldUnregister` to true to unregister input during unmount" — and "By setting `shouldUnregister` to true at useForm level, `defaultValues` will not be merged against submission result." So `true` would silently drop collapsed values.

### 🟠 High
3. **Derive the form schema from generated code** via `.pick()/.partial()/.extend()` so the metric set cannot drift from the backend contract (the exact failure Slice 1B codegen exists to prevent).
4. **Zod v4 `.coerce` input type is now `unknown`** (was specific in v3), and `.default()` placement/short-circuit changed — both bite the resolver typing. Use the three-generic `useForm<z.input<…>, any, z.output<…>>` form whenever a transform/preprocess/coerce makes input ≠ output.
5. **`FormMessage` carries no `role="alert"`** in the repo's current shadcn copy — error text is wired via `aria-describedby`/`aria-invalid` but not announced assertively. The hand-rolled onboarding inputs do carry `role="alert"`; flag for parity (#560).

### 🟡 Medium
6. **Duration: a single numeric "minutes" (decimal) field** for MVP-0 — lowest friction, simplest RHF binding, no segmented-input a11y burden.
7. **History layout: `<dl>` with `<div>`-grouped pairs**, rendering only present metrics, from a single shared key→label/unit map.

### 🔵 Low / housekeeping
8. **`motion-reduce:` pairing** for the Collapsible expand animation (tw-animate-css) per DEC-063.
9. **Auto-expand the optional section when any optional field is pre-filled** (edit/prefill case); default closed on a fresh log.

---

## Details

### SQ1 — Form idiom: RHF vs React 19 Actions → **Stay on RHF + shadcn Form**

React 19 ships `useActionState`, the `form action` prop, and `useFormStatus`. These are genuinely useful for **server-driven** progressive-enhancement forms (especially RSC / Next App Router), but they provide **no client-side validation system** — React's own guidance and the ecosystem consensus is that complex client forms still reach for a validation library. The interop pattern people actually ship uses RHF for client validation and lets Actions handle the server round-trip; in a **Vite SPA with RTK Query as the mutation layer there is no server action to integrate with**, so Actions add ceremony with no payoff.

RHF wins concretely here because: (a) **uncontrolled inputs → fewer re-renders** on a wide form; (b) `useFieldArray` is available for any future splits entry; (c) the repo already standardizes on `useForm({ resolver: zodResolver(schema) })` + shadcn `Form`; (d) Actions' `FormData`-based model is awkward for nested/typed metric objects and a derived Zod schema. **Verdict: no 2026 reason to move to Actions for this surface.**

**Canonical skeleton (core fields + Collapsible optional section):**

```tsx
// logging/components/log-workout-form.tsx
import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Form, FormField, FormItem, FormLabel, FormControl, FormMessage, FormDescription,
} from "@/components/ui/form";
import { Collapsible, CollapsibleTrigger, CollapsibleContent } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Button } from "@/components/ui/button";
import { logFormSchema, type LogFormInput, type LogFormOutput } from "../schema/log-form.schema";
import { OPTIONAL_NUMERIC_FIELDS, OPTIONAL_KEYS } from "../metrics/metric-meta";

export function LogWorkoutForm({ defaultValues, onSubmit }: {
  defaultValues?: Partial<LogFormInput>;
  onSubmit: (values: LogFormOutput) => void;
}) {
  // 3-generic form: input (strings from inputs) ≠ output (coerced numbers / undefined)
  const form = useForm<LogFormInput, any, LogFormOutput>({
    resolver: zodResolver(logFormSchema),
    shouldUnregister: false,           // 🔴 keep collapsed-but-filled values
    defaultValues: { distanceKm: "", durationMin: "", status: "completed", notes: "", ...defaultValues },
  });

  // 🟡 auto-expand if any optional metric is pre-filled (edit / prefill)
  const hasOptional = OPTIONAL_KEYS.some((k) => defaultValues?.[k] != null);
  const [open, setOpen] = useState(hasOptional);
  useEffect(() => { if (hasOptional) setOpen(true); }, [hasOptional]);

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {/* ---- required core fields ---- */}
        <FormField control={form.control} name="distanceKm" render={({ field }) => (
          <FormItem>
            <FormLabel>Distance (km)</FormLabel>
            <FormControl>
              <Input type="text" inputMode="decimal" autoComplete="off" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )} />

        <FormField control={form.control} name="durationMin" render={({ field }) => (
          <FormItem>
            <FormLabel>Duration (minutes)</FormLabel>
            <FormControl><Input type="text" inputMode="decimal" {...field} /></FormControl>
            <FormDescription>Total time in minutes, e.g. 42.5</FormDescription>
            <FormMessage />
          </FormItem>
        )} />

        <FormField control={form.control} name="status" render={({ field }) => (
          <FormItem>
            <FormLabel>Status</FormLabel>
            <FormControl>
              <RadioGroup onValueChange={field.onChange} value={field.value} className="flex gap-4">
                {(["completed", "partial", "skipped"] as const).map((v) => (
                  <FormItem key={v} className="flex items-center gap-2">
                    <FormControl><RadioGroupItem value={v} /></FormControl>
                    <FormLabel className="font-normal capitalize">{v}</FormLabel>
                  </FormItem>
                ))}
              </RadioGroup>
            </FormControl>
            <FormMessage />
          </FormItem>
        )} />

        <FormField control={form.control} name="notes" render={({ field }) => (
          <FormItem>
            <FormLabel>Notes</FormLabel>
            <FormControl><Textarea rows={3} {...field} /></FormControl>
            <FormMessage />
          </FormItem>
        )} />

        {/* ---- optional metrics: progressive disclosure ---- */}
        <Collapsible open={open} onOpenChange={setOpen}>
          <CollapsibleTrigger asChild>
            {/* asChild → real <button>; Radix auto-sets aria-expanded + aria-controls */}
            <Button type="button" variant="ghost">{open ? "Hide details" : "More details"}</Button>
          </CollapsibleTrigger>
          {/* Fields stay MOUNTED inside CollapsibleContent — never {open && …}.
              With shouldUnregister:false, values persist across collapse regardless of mount. */}
          <CollapsibleContent className="space-y-4 data-[state=open]:animate-collapsible-down
                                         data-[state=closed]:animate-collapsible-up
                                         motion-reduce:transition-none motion-reduce:animate-none">
            {OPTIONAL_NUMERIC_FIELDS.map(({ name, label, unit }) => (
              <FormField key={name} control={form.control} name={name} render={({ field }) => (
                <FormItem>
                  <FormLabel>{label}{unit ? ` (${unit})` : ""}</FormLabel>
                  <FormControl>
                    <Input
                      type="text" inputMode="decimal"
                      {...form.register(name, {
                        setValueAs: (v) => (v === "" || v == null ? undefined : Number(v)),
                      })}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )} />
            ))}
          </CollapsibleContent>
        </Collapsible>

        <Button type="submit" disabled={form.formState.isSubmitting}>Save log</Button>
      </form>
    </Form>
  );
}
```

### SQ2 — Zod v4 optional/default/coercion rules → **`setValueAs` + `z.number().optional()`**

**Rule set for Zod 4.4.x:**

- **"User left it blank" = `undefined`.** Use `.optional()` (`T | undefined`). Use `.nullish()` (`T | null | undefined`) only if the backend distinguishes `null`. Do **not** use `.optional().default(0)` for metrics — a default makes "blank" indistinguishable from "explicitly zero."
- **"Explicitly zero" = the number `0`** must arrive as `0`, never as the absence sentinel. This is precisely why `z.coerce.number()` is wrong for optional fields: it turns `""` into `0` (colinhacks/zod #2461), so a blank optional field validates as `0` — a false positive and corrupt data.
- **The NaN problem:** RHF `valueAsNumber` returns `NaN` for an empty `type="number"` input ("valueAsNumber returns NaN if something goes wrong"), and `NaN` fails `z.number().optional()` (NaN is neither a valid number nor `undefined`).

**Canonical empty-numeric → `undefined` pattern.** Two complementary layers; the field-level one is primary because it keeps the schema clean and is the answer endorsed in RHF Discussion #6980:

```ts
// PRIMARY (RHF-level): schema stays z.number().optional()
<Input type="text" inputMode="decimal"
  {...form.register("rpe", { setValueAs: (v) => (v === "" || v == null ? undefined : Number(v)) })} />
```

```ts
// ALTERNATIVE (schema-level): robust regardless of wiring (Controller, server reuse)
const optionalNumber = z.preprocess(
  (v) => (v === "" || v == null ? undefined : v),
  z.number().optional()
);
// preprocess is preferred over .transform() because preprocess runs BEFORE type validation
```

**Zod v4 breaking changes that bite here (state precisely):**

- **`z.coerce.*` input type is now `unknown`** (Zod v4 migration guide: `z.input<typeof z.coerce.string()>` is `unknown` in v4 vs `string` in v3). This surfaces new TS errors when a coerced field feeds `zodResolver`, because the field's `z.input<>` is `unknown` rather than `number` — corroborated by RHF Discussion #13205 (`z.Schema` is now `ZodType<unknown, unknown, …>`).
- **`.default()` placement / short-circuit changed:** in v4 `.default()` must be assignable to the **output** type and short-circuits on `undefined` input; defaults inside optional properties are now applied — `z.object({ a: z.string().default("x").optional() }).parse({})` → `{ a: "x" }` in v4 vs `{}` in v3. Use `.prefault()` for the old "parse the default" behavior. **Implication: avoid `.default()` on optional metrics entirely.**
- **Error API:** `invalid_type_error` / `required_error` / `errorMap` are replaced by a unified `error` param; `.format()` / `.flatten()` are deprecated in favor of `z.treeifyError()`.
- **Resolver typing:** when input ≠ output, supply the three generics. The `@hookform/resolvers` README documents this exact "Force the output type" pattern: `useForm<z.input<typeof schema>, any, z.output<typeof schema>>`.

### SQ3 — Deriving the form schema from generated code → **`.pick()` → `.extend()` (and `.partial()` where needed)**

The Orval-generated request-body schema (e.g. `createLogBody`) is the source of truth for which metrics exist. Build the form schema **from** it so the canonical field set is never hand-maintained:

```ts
// logging/schema/log-form.schema.ts
import * as z from "zod";
import { createLogBody } from "@/app/api/generated/zod";  // via the hand-maintained barrel

// Frontend coercion helper: blank → undefined, then number
const numFromInput = (schema: z.ZodTypeAny) =>
  z.preprocess((v) => (v === "" || v == null ? undefined : Number(v)), schema);

const metricKeys = {
  rpe: true, hrAvg: true, hrMax: true, calories: true,
  hrv: true, sleepScore: true, recoveryScore: true, weather: true, terrain: true,
} as const;

export const logFormSchema = createLogBody
  .pick({ distanceKm: true, durationMin: true, status: true, notes: true, ...metricKeys })
  .extend({
    // frontend-only refinements & input coercion, layered WITHOUT diverging from source
    distanceKm: numFromInput(z.number().positive().max(1000)),
    durationMin: numFromInput(z.number().positive().max(1440)),
    rpe: numFromInput(z.number().int().min(1).max(10).optional()),
    hrAvg: numFromInput(z.number().int().min(20).max(260).optional()),
    hrMax: numFromInput(z.number().int().min(20).max(260).optional()),
    calories: numFromInput(z.number().int().min(0).optional()),
    hrv: numFromInput(z.number().min(0).optional()),
    sleepScore: numFromInput(z.number().min(0).max(100).optional()),
    recoveryScore: numFromInput(z.number().min(0).max(100).optional()),
  });

export type LogFormInput = z.input<typeof logFormSchema>;   // strings/unknown from inputs
export type LogFormOutput = z.output<typeof logFormSchema>; // coerced numbers / undefined
```

**Notes & footguns:**
- In Zod v4, `.pick()/.omit()/.partial()` operate on plain `ZodObject` and **throw if the source schema has refinements** — confirmed against the v4 source: `if (hasChecks) { throw new Error(".pick() cannot be used on object schemas containing refinements"); }`. Keep the generated body schema refinement-free, or pick from a refinement-free base. v4 added `.safeExtend()` / `.safeOmit()` to preserve refinements when needed.
- Prefer `.extend()` over `z.intersection()` — `.extend()` returns a real `ZodObject` retaining `pick`/`omit`/`partial`.
- **Open-ended metrics map (read tolerance):** if the generated schema types metrics as an open record (`z.record(z.string(), z.unknown())` or a loose object), the **write** path (form) binds only the **known canonical subset** (`metricKeys`), while the **read** path (history) stays tolerant of unknown keys. Use `z.looseObject(...)` (the v4 replacement for `.passthrough()`) on the response schema so future server-added metrics don't fail parse; the history renderer then ignores keys it has no label for (or humanizes them). **Never** use `z.strictObject` on the response.

### SQ4 — Progressive-disclosure accessibility → **Radix Collapsible, `shouldUnregister: false`, focus stays on trigger**

shadcn `Collapsible` wraps Radix Collapsible, which "Adheres to the Disclosure WAI-ARIA design pattern." The `CollapsibleTrigger` is a `<button>` that automatically gets `aria-expanded` reflecting state and `aria-controls` pointing at the content region; the content gets `data-state` and is hidden when closed. Per the W3C WAI-ARIA APG Disclosure pattern: "When the content is visible, the element with role button has `aria-expanded` set to true. When the content area is hidden, it is set to false." You do not hand-wire these — Radix manages them. (Confirm the trigger renders a real `<button>` by using `asChild` with shadcn `Button`.)

- **Do collapsed-but-filled fields still submit? Yes — iff `shouldUnregister: false`** (the RHF v7 default). RHF retains unmounted-input values by default; `shouldUnregister: true` makes the form behave like native forms (unmounting removes the value, and `defaultValues` are not merged into submission). **Spec: set `shouldUnregister: false` at `useForm` level, and keep fields mounted inside `CollapsibleContent` — never conditionally render with `{open && …}`.** Relying on `shouldUnregister: false` is the load-bearing guarantee independent of Radix's mount behavior.
- **Focus management:** this is a **disclosure, not a dialog** — do **not** trap or forcibly move focus into the region on expand. Disclosure guidance keeps focus on the trigger; the newly revealed fields become the next tab stops. Forcible focus movement is appropriate only for modal patterns. Keyboard: Enter/Space toggle (native button behavior).
- **Auto-expand if filled:** **default closed on a fresh log** (athletes log the bare minimum fast; closed reduces cognitive load — NN/g and broader progressive-disclosure research support deferring rarely-used options). **Auto-open if any optional field has a value** (edit/prefill, or future NL-parse prefill) so populated metrics are visible and correctable. This is a bounded UX choice; the accessible default is *closed*, and the trigger label ("More details" / "Hide details") communicates state in addition to `aria-expanded`.

### SQ5 — Accessible numeric & duration inputs → **`type="text" inputMode="decimal"`; single "minutes" field for duration**

For distance and all numeric metrics, prefer **`<input type="text" inputMode="decimal">`** over `type="number"`:
- MDN: the implicit role of `type="number"` is `spinbutton`; "if spinbutton is not an important feature for your form control, consider not using `type=number`. Instead, use `inputmode=numeric` along with a `pattern`."
- The GOV.UK Design System team moved away from `type="number"` after user testing — per their Feb 2020 engineering post they switched to `<input type="text" inputmode="numeric" pattern="[0-9]*">` because "we identified many usability problems with this input type" (spinner mis-increments, zoom, autofill, and locale decimal-separator inconsistencies). `pattern` is not even supported on `type="number"`.
- `inputMode="decimal"` surfaces the correct mobile keypad (digits + locale decimal separator) without spinbutton footguns. Validation is owned by Zod, not the browser.

**Duration:** use a **single numeric "minutes" (decimal) field** (e.g. `42.5`). Rationale: lowest friction for runners, one labeled field (trivially accessible), one RHF binding, and no segmented-input ARIA complexity. An `mm:ss` masked/segmented input or separate h/m/s fields add input-handling and screen-reader burden for marginal benefit at MVP-0. Revisit a two-field `min`/`sec` pattern only if sub-minute precision becomes a hard requirement.

**RHF binding (corrects the repo's existing `Controller` + `valueAsNumber` for the optional case):**

```tsx
<FormField control={form.control} name="hrAvg" render={({ field }) => (
  <FormItem>
    <FormLabel>Avg HR (bpm)</FormLabel>
    <FormControl>
      <Input type="text" inputMode="decimal"
        {...form.register("hrAvg", {
          setValueAs: (v) => (v === "" || v == null ? undefined : Number(v)),  // replaces valueAsNumber
        })} />
    </FormControl>
    <FormMessage />
  </FormItem>
)} />
```

For controlled `Controller` usage (the repo's turn-input style), apply the same blank→undefined transform in the field's value/`onChange` mapping rather than `valueAsNumber` (which yields `NaN`).

### SQ6 — Heterogeneous sparse-metric rendering → **`<dl>` with `<div>`-grouped pairs, only present metrics**

Use a **description list** (`<dl>` + `<dt>`/`<dd>`), wrapping each pair in a `<div>` for flex/grid layout (the HTML spec explicitly allows `<dl><div><dt>…</dt><dd>…</dd></div></dl>`):

- **Why `<dl>` over a pill/badge grid or table:** `<dl>` is the semantically correct element for key–value metadata. Screen readers can announce "description list," convey the term↔value relationship, and let users skip the block. A badge grid is divs (no programmatic key/value relationship → WCAG 1.3.1 weakness). A two-column table implies row/column relationships that don't exist for a flat metric set and adds DOM weight.
- **No empty-cell noise:** render only metrics where `value != null`. Because entries are sparse, you iterate the present keys, not a fixed grid — absent metrics produce no DOM at all.

```tsx
// logging/components/log-metrics.tsx
import { METRIC_META, formatMetric } from "../metrics/metric-meta";

export function LogMetrics({ metrics, unitPref }: { metrics: Record<string, unknown>; unitPref: UnitPref }) {
  const present = Object.entries(metrics).filter(([, v]) => v != null);
  if (present.length === 0) return null;
  return (
    <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
      {present.map(([key, value]) => (
        <div key={key} className="flex justify-between gap-2">
          <dt className="text-muted-foreground">{METRIC_META[key]?.label ?? key}</dt>
          <dd className="font-medium tabular-nums">{formatMetric(key, value, unitPref)}</dd>
        </div>
      ))}
    </dl>
  );
}
```

**Single shared key→label/unit map** (consumed by both form labels and history):

```ts
// logging/metrics/metric-meta.ts
export const METRIC_META = {
  rpe:           { label: "RPE",         unit: "" },
  hrAvg:         { label: "Avg HR",      unit: "bpm" },
  hrMax:         { label: "Max HR",      unit: "bpm" },
  calories:      { label: "Calories",    unit: "kcal" },
  hrv:           { label: "HRV",         unit: "ms" },
  sleepScore:    { label: "Sleep score", unit: "" },
  recoveryScore: { label: "Recovery",    unit: "" },
  weather:       { label: "Weather",     unit: "" },
  terrain:       { label: "Terrain",     unit: "" },
} as const satisfies Record<string, { label: string; unit: string }>;

export const OPTIONAL_KEYS = Object.keys(METRIC_META) as (keyof typeof METRIC_META)[];
export const OPTIONAL_NUMERIC_FIELDS = (["rpe","hrAvg","hrMax","calories","hrv","sleepScore","recoveryScore"] as const)
  .map((name) => ({ name, label: METRIC_META[name].label, unit: METRIC_META[name].unit }));
```

`hrAvg → "Avg HR"` thus lives in exactly one place. Units render per the user's unit preference (km/mi, etc.) inside `formatMetric`.

### SQ7 — Splits rendering → **Display-only in MVP-0; no `useFieldArray`**

**MVP-0 verdict: splits are DISPLAY-ONLY.** Manual entry of 10+ lap rows is exactly the high-friction surface the product is trying to avoid; splits realistically arrive from a future wearable/file import, not hand entry. Therefore:
- **Do not** build `useFieldArray` entry for MVP-0 — keep the form simpler.
- In history, splits must **not bloat the row.** Render a one-line summary (e.g. "8 splits · avg 4:52/km") and put detail behind an **expandable nested `Collapsible`** containing a compact `<table>`. Laps are genuinely tabular (lap #, distance, duration, pace → real row/column semantics), so a `<table>` with `<th scope="col">` is correct *here*, unlike the flat metric set.
- The splits region uses its own Collapsible (Disclosure pattern; `aria-expanded`/`aria-controls` via Radix). Lazy-render the table on first open to keep long lists light.

If/when splits become user-entered, the `useFieldArray` sketch is:

```tsx
const { fields, append, remove } = useFieldArray({ control: form.control, name: "splits" });
// fields.map((field, i) => <Controller key={field.id} name={`splits.${i}.paceSec`} … />)
// append({ distanceKm: undefined, durationSec: undefined })  // must pass ALL field defaults, never {}
```
(RHF: use `field.id` as the key, never the index; `append({})` is invalid — supply all field defaults.)

### SQ8 — Create → appears-in-history data flow → **Pessimistic (await → invalidate `History` LIST tag → refetch)**

**Recommendation: pessimistic update.** For a single-user MVP-0 logging surface:
- The repo's only existing pattern (regenerate-plan) is already pessimistic; consistency lowers cognitive/maintenance cost.
- The **idempotency-key + "try again" contract** (R-078's retryable flag) means a submit can legitimately be retried; an optimistic insert would have to be reconciled against a possibly-different server-canonical record (server assigns `id`, timestamps, derived fields) and rolled back/replaced on the retry path — extra complexity for a list the user is staring at for ~1 second.
- A run-log create is a deliberate, once-per-run action — not a high-frequency interaction where optimism materially improves perceived performance. One POST + one refetch is acceptable latency.

```ts
createLog: build.mutation<LogResponse, CreateLogBody>({
  query: (body) => ({
    url: "/logs", method: "POST", body,
    headers: { "Idempotency-Key": body.idempotencyKey },
  }),
  invalidatesTags: [{ type: "History", id: "LIST" }],
}),
getHistory: build.query<LogResponse[], void>({
  query: () => "/logs",
  providesTags: (result) =>
    result
      ? [...result.map(({ id }) => ({ type: "History" as const, id })), { type: "History", id: "LIST" }]
      : [{ type: "History", id: "LIST" }],
}),
```

On success the `History`/`LIST` tag invalidates and the active history subscription refetches, so the new log appears with its server-canonical shape. **Defer optimistic** until a measured latency problem appears. If adopted later, use `onQueryStarted` + `updateQueryData` (insert into the list draft) with rollback in `catch` — but RTK docs warn that with overlapping mutations, **invalidating tags on error is safer than `patchResult.undo()`**. Given the idempotency/retry contract, invalidate-on-error is the better rollback strategy even in the optimistic case.

### SQ9 — Slice 2a design-system compliance checklist

- **Semantic tokens only:** all colors via tokens (`text-muted-foreground`, `text-foreground`, `bg-card`, `text-destructive`, …). No hardcoded hex. History `<dt>` uses `text-muted-foreground`; values use `text-foreground`.
- **check-contrast WCAG gate:** reusing existing semantic tokens introduces no new color → no new contrast verification. **If** a new token is added (e.g. a metric accent), it must pass the pre-commit/CI contrast gate in **both** Latte and Mocha.
- **Class-based dark mode:** components must not assume light; use tokens that flip via `ThemeProvider`. Verify Collapsible chevron/triggers in Mocha.
- **DEC-063 reduced-motion:** the Collapsible expand/collapse uses tw-animate-css (`animate-collapsible-down/up`). **Every `transition-*`/`animate-*` must be paired with a `motion-reduce:` variant** — here `motion-reduce:animate-none motion-reduce:transition-none`. Tailwind's `motion-reduce:` maps to `@media (prefers-reduced-motion: reduce)`. Same applies to the splits Collapsible and any submit-button spinner (`animate-spin motion-reduce:hidden`, or a static state). Since DEC-063 is review-enforced (no lint rule yet), call these pairings out explicitly in the PR.
- **`FormMessage` `role="alert"` gap (🟠):** the repo's shadcn `FormMessage` renders a `<p>` with `aria-describedby` wiring but **no `role="alert"`**, so validation errors are associated but not assertively announced on submit (the hand-rolled onboarding inputs do carry `role="alert"`). For parity and better SR feedback, consider adding `role="alert"` (or an `aria-live="polite"` region) to `FormMessage` — a deliberate, reviewable change to the shared component, tracked alongside #560.

---

## Recommendations (staged, mechanical)

**Stage 1 — Schema & coercion foundation (do first; unblocks everything):**
1. Create `log-form.schema.ts` deriving from `createLogBody` via `.pick().extend()`; export `LogFormInput`/`LogFormOutput`.
2. Implement `numFromInput` (blank→undefined preprocess) and/or per-field `setValueAs`. **Benchmark:** the minimum payload (all metrics blank) must `safeParse` to success with every metric `undefined`; an explicit `0` must survive as `0`.
3. Wire `useForm<LogFormInput, any, LogFormOutput>` with `shouldUnregister: false`.

**Stage 2 — Form UI:**
4. Build core fields + `Collapsible` optional section (fields mounted inside; never `{open && …}`).
5. Add auto-expand-if-filled; default closed otherwise.
6. Use `type="text" inputMode="decimal"` everywhere numeric; single "minutes" duration field.

**Stage 3 — History & data flow:**
7. Build `metric-meta.ts`; render history with `<dl>` showing only present metrics.
8. Splits = display-only summary + nested Collapsible `<table>` (lazy-rendered).
9. Add `createLog` (pessimistic, idempotency header, invalidates `History/LIST`) + `getHistory` (provides LIST).

**Stage 4 — Compliance pass:**
10. Audit tokens, dark mode, and `motion-reduce:` pairings; run check-contrast.
11. Decide on `FormMessage` `role="alert"` (recommend adding; coordinate with #560).

**Thresholds that change the call:**
- If create→appear latency is perceptibly slow in testing (felt >~1s), switch to optimistic insert with **invalidate-on-error** rollback.
- If sub-minute duration precision is required, move to a two-field min/sec pattern.
- If splits become user-entered, add `useFieldArray` (Stage 2 add-on) per the sketch.
- If the backend types metrics as an open record, ensure the **read** schema uses `z.looseObject` so unknown keys survive parse.

## Caveats

- **Source hygiene:** version-specific behavior — Zod v4 `.coerce`→`unknown` input (Zod v4 migration guide; corroborated RHF Discussion #13205), `.default()` short-circuit, `.pick()` throwing on refinements (Zod v4 `core/util.ts`) — is anchored to official Zod docs and source. RHF `shouldUnregister` semantics are from the official `useForm` docs and maintainer (bluebill1049) statements; the empty-numeric `setValueAs` pattern is the answer endorsed in RHF Discussion #6980. The `z.coerce.number()` `""→0` footgun is from colinhacks/zod Issue #2461. RTK optimistic/pessimistic and tag-invalidation patterns are from official Redux Toolkit docs. Radix Collapsible's Disclosure conformance is from the official Radix Primitives docs and the W3C WAI-ARIA APG.
- **`<dl>` screen-reader support is good but not universal** (notably older VoiceOver may not announce "list"); mitigation: each `<dt>`/`<dd>` pair reads sensibly standalone (e.g. "Avg HR 152"). If audit reveals a blocking SR issue, fall back to a `<ul>` of "Label: value" items — still semantic, weaker key/value association.
- **`z.coerce` vs `setValueAs`/`preprocess`:** the artifact deliberately avoids `z.coerce.number()` for optional fields because of the `""→0` footgun; if any field legitimately wants `0` as a blank default, that must be explicit, not a coercion side effect.
- **Radix mount behavior:** verify against the installed `radix-ui` 1.4.3 build whether Collapsible toggles `hidden` vs unmounts; regardless, `shouldUnregister: false` is the load-bearing guarantee for value retention, so the recommendation holds either way.
- **Not covered (out of scope):** backend JSONB metric persistence (R-077), the LLM-failure retry envelope (R-078, consumed only as the retryable flag), metric coaching semantics (batch-3c), edit/delete of logs (deferred), NL "log my run" parsing, and the token architecture itself (DEC-070).