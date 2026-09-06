# Slice 6 design source extract

Status: read-only recon. This is a first-draft implementation evidence file.

## Match method

- `2f` is matched by its `id` and `data-screen-label=2f Settings`. Source: `docs/design/split-alpine/split-alpine.dc.html:2055-2061`.
- `2g` is matched by its `id` and `data-screen-label=2g Sign in`. Source: `docs/design/split-alpine/split-alpine.dc.html:2148-2155`.
- `5e` is matched by its `id` and `data-screen-label=5e Daylight [EM DASH] Settings`. Source: `docs/design/split-alpine/split-alpine.dc.html:603-609`.
- `5f` is matched by its `id` and `data-screen-label=5f Daylight [EM DASH] Sign in`. Source: `docs/design/split-alpine/split-alpine.dc.html:697-704`.
- `5h` is a container with two sub-artboards. This extract uses only `data-screen-label=5h Regenerate dialog`, not the unrelated splits-expander panel. Source: `docs/design/split-alpine/split-alpine.dc.html:774-807`.
- No register artboard exists. Searches find only Sign in frames. `START HERE`, password rules, and register-specific visible copy occur only in HANDOFF. Source: `docs/design/split-alpine/split-alpine.dc.html:697-741,2148-2187`; `docs/design/split-alpine/HANDOFF.md:127-128`.

## Character map and TSX rule

| Source character | TSX literal escape | Use |
| --- | --- | --- |
| [EM DASH] | `\u2014` | Footer and descriptive prose |
| [MIDDLE DOT] | `\u00B7` | Current-plan metadata |
| [ARROW] | `\u2192` | Create-account link |
| [ELLIPSIS] | `\u2026` | Only if a supplied string needs it |

Use these escapes in TSX literals. Keep authored source copy sentence case when feasible and apply uppercase in CSS. Source: `frontend/CLAUDE.md:198-209`; `docs/design/split-alpine/HANDOFF.md:18`.

## Ordered copy table

The table includes visible application-frame strings. Design-document labels such as `2f` and its route note are included separately because they are visible in the source page but are not app UI.

### 2f Settings, dark

| Order | Visible string | Notes |
| --- | --- | --- |
| Source label | `2f` | Design-page badge. |
| Source note | `/settings [EM DASH] plan, appearance, units, account` | Design-page annotation. |
| 1 | `07:15` | Device status mock. |
| 2 | `SETTINGS` | Screen title. |
| 3 | `THE PLAN` | Rule-section heading. |
| 4 | `CURRENT PLAN` | Mono eyebrow. |
| 5 | `Generated Jun 29, 2026 [MIDDLE DOT] 12 weeks [MIDDLE DOT] Portland 10K` | Dynamic target shape: `Generated {date} \u00B7 {weeks} weeks \u00B7 {goal}`. |
| 6 | `REGENERATE PLAN` | Outline CTA. |
| 7 | `REPLACES YOUR CURRENT PLAN. THE COACH STARTS FRESH FROM YOUR LOG BOOK.` | Warning text. |
| 8 | `APPEARANCE` | Rule-section heading. |
| 9 | `DARK` | Selected segment in shown state. |
| 10 | `LIGHT` | Unselected segment. |
| 11 | `SYSTEM` | Unselected segment. |
| 12 | `UNITS` | Rule-section heading. |
| 13 | `KILOMETERS` | Selected segment in shown state. |
| 14 | `MILES` | Unselected segment. |
| 15 | `ACCOUNT` | Rule-section heading. |
| 16 | `SIGNED IN AS` | Mono eyebrow. |
| 17 | `<signed-in email>` | Decoded from the source HTML email protection data. |
| 18 | `SIGN OUT` | Outline control. |
| 19 | `SPLIT 0.9.0 [EM DASH] MVP` | Build-version footer shape. |
| 20 | `TODAY` | Tab-bar label. |
| 21 | `COACH` | Tab-bar label. |
| 22 | `LOG BOOK` | Tab-bar label. |
| 23 | `SETTINGS` | Active tab-bar label. |

Source: `docs/design/split-alpine/split-alpine.dc.html:2057-2120,2124-2144`.

### 5e Settings, light

The ordered application copy is identical to 2f, including the dynamic plan shape, account email, footer, and tab labels. Source: `docs/design/split-alpine/split-alpine.dc.html:605-664,667-692`.

Visible source-only labels are `5e` and `DAYLIGHT [MIDDLE DOT] /settings [EM DASH] plan, appearance, units, account`. Source: `docs/design/split-alpine/split-alpine.dc.html:603-606`.

### 2g Sign in, dark

| Order | Visible string | Notes |
| --- | --- | --- |
| Source label | `2g` | Design-page badge. |
| Source note | `/login [EM DASH] brand moment (3rd-party auth is a fast-follow; room left below the button)` | Design-page annotation. |
| 1 | `05:42` | Device status mock. |
| 2 | `SPLIT/` | Wordmark, accessible name must remain `Split`. |
| 3 | `THE PLAN ADAPTS. YOU DO THE WORK.` | Mono tagline. |
| 4 | `EMAIL` | Input label. |
| 5 | `<signed-in email>` | Example field value decoded from source data. |
| 6 | `PASSWORD` | Input label. |
| 7 | `[BULLET]` repeated 12 times | Masked password display in mock. |
| 8 | `SIGN IN` | Filled primary CTA. |
| 9 | `First run here?` | Body copy. |
| 10 | `CREATE ACCOUNT [ARROW]` | Register link. |

Source: `docs/design/split-alpine/split-alpine.dc.html:2151-2185`.

### 5f Sign in, light

The ordered application copy is identical to 2g. Source: `docs/design/split-alpine/split-alpine.dc.html:700-734`.

Visible source-only labels are `5f` and `DAYLIGHT [MIDDLE DOT] /login [EM DASH] brand moment (3rd-party auth is a fast-follow; room left below the button)`. Source: `docs/design/split-alpine/split-alpine.dc.html:698-701`.

### Register

Only these register instructions are locked by the handoff: the poster mirrors sign in, it has a `START HERE` heading, it renders a password-rules helper in mono, and it reserves vertical room under the primary button for an OAuth fast-follow. Exact visible password rules, labels, CTA copy, and the reservation height are not in the design source. Source: `docs/design/split-alpine/HANDOFF.md:127-128`.

### 5h Regenerate dialog

| Order | Visible string | Notes |
| --- | --- | --- |
| Source label | `5h` | Design-page badge. |
| Source note | `Splits expander (Log Book) + Regenerate dialog (Settings)` | Design-page annotation. |
| Backdrop 1 | `SETTINGS` | Faded underlying settings screen. |
| Backdrop 2 | `REGENERATE PLAN` | Faded underlying trigger. |
| 1 | `REGENERATE PLAN` | Dialog title. |
| 2 | `This replaces your current plan. The coach starts fresh from your log book [EM DASH] nothing you've logged is lost.` | Dialog description. |
| 3 | `ANYTHING I SHOULD KNOW? [EM DASH] OPTIONAL` | Textarea label. |
| 4 | `Coming back from a calf strain [EM DASH] keep the first two weeks conservative.` | Example textarea content. |
| 5 | `431 LEFT` | Counter display. HANDOFF, not the artboard text, establishes the 500-character maximum. |
| 6 | `CANCEL` | Secondary action. |
| 7 | `REGENERATE` | Filled primary action. |

Source: `docs/design/split-alpine/split-alpine.dc.html:774-777,807-827`; `docs/design/split-alpine/HANDOFF.md:124-125`.

## Component map

| Element | Component | Required composition and variant |
| --- | --- | --- |
| Settings screen title | Native `h1` | `t-screen-title text-foreground`, then a separate full-width 2px `bg-rule` divider with 10px block gap. Source: `docs/design/split-alpine/split-alpine.dc.html:2067-2072`; `frontend/src/index.css:560-582`. |
| Settings rule headings | `SectionRule` | Sentence-case `label` props such as `The plan`; component provides `border-t-2 border-rule pt-2` and `t-section-label`. Source: `section-rule.component.tsx:28-44`. |
| Current-plan and account eyebrows | `MonoLabel` | Use `tone=muted`; design-polarity faint is not safe for essential information. Source: `mono-label.component.tsx:4-35`; `frontend/CLAUDE.md:148-156`. |
| Current-plan data and email | Native text | `t-body text-muted-foreground`; design is 14.5px Barlow regular. Source: `split-alpine.dc.html:2080,2114`; `index.css:595-601`. |
| Regenerate trigger | `Button` | `variant=outline`, height 48px, clay text and clay border override, active secondary fill from shared primitive. Source: `split-alpine.dc.html:2082`; `button.tsx:16-17`. |
| Theme selector | `SegmentedControl` and three `SegmentedControlItem` children | Controlled value from `useTheme()`. Use Dark, Light, System source strings with presentation uppercase. Source: `segmented-control.tsx:6-45`; `frontend/CLAUDE.md:168-174`. |
| Units selector | `SegmentedControl` and two `SegmentedControlItem` children | Controlled by existing units settings wiring. Source: `split-alpine.dc.html:2101-2104`; `segmented-control.tsx:6-45`. |
| Sign out | `Button` | Visually the artboard is an outline secondary control. Use `variant=outline` with a 44px minimum target, rather than copying the shown 40px height. Source: `split-alpine.dc.html:2116`; `button.tsx:16-29`. |
| Auth mark | `Wordmark size=poster` | Existing component supplies 58px, tracked SPLIT, pulled-back slash, clay text, and accessible name `Split`. Source: `wordmark.component.tsx:3-50`. |
| Auth rule | Native divider | `h-0.5 w-16 bg-rule`. Source: `split-alpine.dc.html:2163-2168`. |
| Auth tagline and field labels | Native text or `MonoLabel` | Tagline is mono 13px weight 500 tracking 0.1em. Labels are mono 10px weight 500 tracking 0.1em. Source: `split-alpine.dc.html:2168,2173,2177`. |
| Email and password fields | `Input` | Add 48px visual height and 14px horizontal padding. Existing Input supplies semantic fill, border, focus, invalid, disabled, and reduced-motion behavior. Source: `split-alpine.dc.html:2172-2178`; `input.tsx:5-20`. |
| Password eye | Native `button type=button` with icon | Use a 44px hit target inside the 48px field. The mock only sizes the icon at 17px. Source: `split-alpine.dc.html:2178`; `frontend/CLAUDE.md:213-226`. |
| Auth primary | `Button variant=default` | Override to 52px, 17px, tracking 0.14em. Source: `split-alpine.dc.html:2180`; `button.tsx:12-13`. |
| Create account route link | Router link styled as text action | Body prompt plus condensed clay-text CTA. Use `\u2192`. Source: `split-alpine.dc.html:2183-2185`. |
| Regenerate modal | `Dialog`, `DialogContent`, `DialogTitle`, `DialogDescription`, `DialogFooter` | Override generic content geometry to 350px at this mock width, 18px padding, 12px gap, 12px radius. Source: `split-alpine.dc.html:813-826`; `dialog.tsx:44-132`. |
| Regenerate intent | Existing form textarea contract | 88px minimum visual height, 11px vertical and 14px horizontal padding, separate end-aligned mono counter. Source: `split-alpine.dc.html:817-823`. |
| Regenerate loading | `BuildingPlanSurface` | Replace the screen while the mutation is active. It announces status and has an indeterminate, reduced-motion-aware bar. Source: `building-plan-surface.component.tsx:15-55`; `frontend/CLAUDE.md:235-241`. |

## Geometry and Tailwind guidance

### Settings

- Screen content: `screen-gutter flex flex-col gap-5 pt-2.5 pb-6.5`. The mock uses 22px horizontal padding, 20px main gap, and 26px bottom padding. Source: `docs/design/split-alpine/split-alpine.dc.html:2067`.
- Header: `flex flex-col gap-2.5`. Use the 30px `t-screen-title`, then a 2px full-width rule. Source: `docs/design/split-alpine/split-alpine.dc.html:2069-2072`.
- Each named section: `flex flex-col gap-3`; `SectionRule` supplies the 2px top rule and 8px top padding. Source: `docs/design/split-alpine/split-alpine.dc.html:2074-2109`; `frontend/src/app/modules/common/components/section-rule/section-rule.component.tsx:34-38`.
- Plan metadata: `flex flex-col gap-0.5`. The label is 10px mono at 0.08em. The value is 14.5px Barlow. Source: `docs/design/split-alpine/split-alpine.dc.html:2078-2080`.
- Regenerate trigger: `h-12 rounded-md`; title treatment is 15px condensed bold at 0.12em. Source: `docs/design/split-alpine/split-alpine.dc.html:2082`.
- Warning: 9.5px mono at 0.06em. Do not retain artboard faint as an essential-text color. Source: `docs/design/split-alpine/split-alpine.dc.html:2083`; `frontend/CLAUDE.md:155-156`.
- Theme grid: `grid grid-cols-3 gap-2`. Units grid: `grid grid-cols-2 gap-2`. Shared segment height is 44px and radius is 8px. Source: `docs/design/split-alpine/split-alpine.dc.html:2090-2104`.
- Account row: `flex items-center justify-between`. The label/value stack has 2px gap. The mock action is 40px, but implementation must supply `min-h-11`. Source: `docs/design/split-alpine/split-alpine.dc.html:2111-2116`; `frontend/CLAUDE.md:223-225`.
- Footer: center aligned, 8px top padding, 9.5px mono, 0.1em tracking. Its source color has no semantic mapping. Source: `docs/design/split-alpine/split-alpine.dc.html:2120`.

### Auth poster

- Frame: 390px by 844px in the source. Main content is vertically centered. Source: `docs/design/split-alpine/split-alpine.dc.html:2155,2161`.
- Poster inner column: `flex flex-1 flex-col justify-center gap-[26px] px-[26px] pb-10`. Source: `docs/design/split-alpine/split-alpine.dc.html:2161`.
- Brand block: `flex flex-col gap-3`. Wordmark is 58px weight 800; rule is `h-0.5 w-16`; tagline is 13px mono weight 500 tracking 0.1em. Source: `docs/design/split-alpine/split-alpine.dc.html:2162-2168`.
- Form stack: `flex flex-col gap-3.5`. Each field stack has 6px gap. Source: `docs/design/split-alpine/split-alpine.dc.html:2171-2178`.
- Labels: 10px mono weight 500 tracking 0.1em. Fields: `h-12 rounded-md px-[14px]`. Source: `docs/design/split-alpine/split-alpine.dc.html:2172-2178`.
- Primary: `h-[52px] rounded-md text-[17px] tracking-[0.14em]`. Source: `docs/design/split-alpine/split-alpine.dc.html:2180`.
- Account link row follows the form as the next 26px-gapped block. The source does not separately measure OAuth vacancy below primary. Source: `docs/design/split-alpine/split-alpine.dc.html:2161,2183-2185`.

### Regenerate dialog

- Demo frame: 390px by 560px. Backdrop uses 20px horizontal inset before content. The actual modal panel is therefore 350px wide in this frame. Source: `docs/design/split-alpine/split-alpine.dc.html:807,813-814`.
- Panel: `flex flex-col gap-3 rounded-xl border border-border bg-card p-[18px]`. The 12px panel radius is the in-screen ceiling. Source: `docs/design/split-alpine/split-alpine.dc.html:814`; `frontend/CLAUDE.md:157-162`.
- Dialog title: 20px condensed bold with 0.06em tracking. Description: 13.5px Barlow regular, line height 1.55. Source: `docs/design/split-alpine/split-alpine.dc.html:815-816`.
- Intent group: `flex flex-col gap-1.5`. Textarea: `min-h-[88px] rounded-md px-[14px] py-[11px]`. Counter is `self-end`, 9.5px mono. Source: `docs/design/split-alpine/split-alpine.dc.html:817-823`.
- Actions: `flex items-center justify-end gap-2.5`. Both controls are 44px. Cancel has 12px horizontal padding. Regenerate has 18px horizontal padding. Source: `docs/design/split-alpine/split-alpine.dc.html:824-826`.

## Semantic color map

Use semantic classes only. Do not carry source literals into implementation.

| Mock use | Semantic token or Tailwind semantic utility | Notes |
| --- | --- | --- |
| Dark and light page background | `bg-background` | Polarity swaps through the existing semantic layer. Source: `frontend/CLAUDE.md:125-141`; `frontend/src/index.css:257-299`. |
| Dialog panel | `bg-card text-card-foreground` | The 5h panel is a raised dialog surface. Source: `docs/design/split-alpine/split-alpine.dc.html:814`; `frontend/src/index.css:260-264`. |
| Main text and section rules | `text-foreground`, `border-rule`, `bg-rule` | Rules are the project-owned semantic slot. Source: `frontend/CLAUDE.md:142-147`; `frontend/src/index.css:313-317`. |
| Secondary body text | `text-muted-foreground` | Use for plan value, email, tagline, and description. Source: `frontend/src/index.css:272-273`; `docs/design/split-alpine/split-alpine.dc.html:2080,2114,2168`. |
| Clay fill and text on fill | `bg-primary text-primary-foreground` | Use for selected segments and primary actions. Source: `frontend/src/index.css:266-267`; `docs/design/split-alpine/split-alpine.dc.html:2091,2102,2180`. |
| Clay text and clay outline | `text-clay-text` plus `border-clay-text` | Use for regenerate outline and create-account CTA. Source: `frontend/src/index.css:315,475-476`; `docs/design/split-alpine/split-alpine.dc.html:2082,2185`. |
| Resting input fill and boundary | `bg-input-fill border-input` | Existing Input makes this distinction. Do not use a raw divider color for a form field. Source: `frontend/src/index.css:297-317`; `frontend/src/components/ui/input.tsx:10-15`. |
| General divider and dialog border | `border-border` | Use only for non-input hairlines. Source: `frontend/src/index.css:297-299`; `docs/design/split-alpine/split-alpine.dc.html:814`. |
| Selected-control pressed state | `bg-clay-pressed` | Required instead of the artboard's old pressed literal. Source: `frontend/CLAUDE.md:213-220`; `frontend/src/index.css:153-172`. |
| Outline or secondary pressed state | `bg-secondary text-secondary-foreground` | Shared Button and SegmentedControl state law. Source: `frontend/src/components/ui/button.tsx:16-21`; `frontend/src/components/ui/segmented-control.tsx:37`. |
| Focus | `border-ring ring-[3px] ring-ring/[0.22]` | This is the frontend canonical focus ring. Source: `frontend/CLAUDE.md:218-222`; `button.tsx:7-8`; `input.tsx:12`. |
| Invalid field and error text | `border-destructive ring-destructive/[0.22] text-destructive` | Preserve FormMessage role-alert wiring. Source: `frontend/CLAUDE.md:221-222`; `docs/design/split-alpine/HANDOFF.md:132`. |
| Essential label/warning mock color | No safe direct mapping from the mock | Mock uses the faint ramp. Replace with `text-muted-foreground`; faint is decorative-only. Source: `docs/design/split-alpine/split-alpine.dc.html:2079,2083`; `frontend/CLAUDE.md:155-156`. |
| Footer mock color | Gap: no semantic slot identified | Do not introduce a raw color. Obtain a semantic-token decision or use an approved existing semantic text token. Source: `docs/design/split-alpine/split-alpine.dc.html:2120`; `frontend/src/index.css:257-358`. |
| Modal scrim | Gap: fixed overlay is intentional, not a themed semantic surface | Use the existing DialogOverlay unless the visual opacity needs an approved adjustment. Source: `frontend/src/components/ui/dialog.tsx:29-40`. |

## States to implement

- Filled primary and selected segment: rest, pressed with `active:scale-[0.98]`, disabled at 35 percent opacity, 3px semantic focus ring, and invalid state where applicable. Source: `frontend/CLAUDE.md:213-226`; `frontend/src/components/ui/button.tsx:7-39`; `frontend/src/components/ui/segmented-control.tsx:34-43`.
- Outline regenerate and sign out: rest plus secondary-fill pressed treatment. Source: `docs/design/split-alpine/split-alpine.dc.html:2082,2116`; `docs/design/split-alpine/split-alpine.dc.html:969-972`.
- Inputs: rest, focus, error with mono error message and existing role-alert wiring. Source: `docs/design/split-alpine/split-alpine.dc.html:975-981`; `docs/design/split-alpine/HANDOFF.md:132`.
- Password visibility: the eye is visible in both auth mocks. The alternate hidden or shown icon is not designed. Source: `docs/design/split-alpine/split-alpine.dc.html:727,2178`.
- Regenerate in flight: replace the screen with `BuildingPlanSurface`, not a closed dialog or a fake percent bar. Source: `docs/design/split-alpine/HANDOFF.md:133`; `frontend/src/app/modules/common/components/building-plan-surface/building-plan-surface.component.tsx:15-55`.
- Every transition or dialog enter/exit must retain a `motion-reduce` pair. Source: `frontend/CLAUDE.md:252-259`; `frontend/src/components/ui/dialog.tsx:36,58`.

## Handoff and artboard disagreements

1. Focus geometry conflicts. HANDOFF says 2px clay outline with 2px offset. The 4b artboard shows clay border plus 3px ring. Follow the existing frontend canonical 3px semantic ring. Sources: `docs/design/split-alpine/HANDOFF.md:132,136`; `docs/design/split-alpine/split-alpine.dc.html:978-979`; `frontend/CLAUDE.md:218-220`.
2. HANDOFF requires all interactive targets to be at least 44px. The 2f and 5e SIGN OUT mock is 40px high. Raise it to `min-h-11`. Sources: `docs/design/split-alpine/HANDOFF.md:136`; `docs/design/split-alpine/split-alpine.dc.html:664,2116`.
3. HANDOFF describes Register, START HERE, mono password rules, and OAuth reservation. No matching register frame appears in the supplied artboard source. This is missing design detail rather than copy that can be inferred. Sources: `docs/design/split-alpine/HANDOFF.md:128`; `docs/design/split-alpine/split-alpine.dc.html:697-741,2148-2187`.
4. HANDOFF preserves a 500-character intent and counter. The 5h mock displays only `431 LEFT`, not the maximum or counter syntax at zero. Preserve 500 as the behavior contract. Sources: `docs/design/split-alpine/HANDOFF.md:125`; `docs/design/split-alpine/split-alpine.dc.html:822`.

## Required recorded deviations from mock

- Do not use artboard faint for CURRENT PLAN, the plan warning, SIGNED IN AS, textarea counter, or password-eye functional affordance. These are essential information or controls, while frontend rules reserve faint for decorative use. Sources: `docs/design/split-alpine/split-alpine.dc.html:2079,2083,2113,2178`; `frontend/CLAUDE.md:155-156`.
- Do not use the handoff and 4b pressed literal. Use `bg-clay-pressed`, which exists because the old literal fails AA with primary foreground. Sources: `docs/design/split-alpine/HANDOFF.md:131`; `docs/design/split-alpine/split-alpine.dc.html:969`; `frontend/src/index.css:153-172`.
- Sign out must be at least 44px high. The password-eye button must receive a 44px target even though the visible icon remains 17px. Sources: `docs/design/split-alpine/split-alpine.dc.html:664,727`; `frontend/CLAUDE.md:223-225`.

## Open questions

1. What are the exact Register password-rule strings, field order, submit label, account-link copy, and error placements?
2. What exact vertical height is reserved for OAuth below the auth primary button?
3. Should the footer receive a new muted/decorative semantic slot, or should it use an existing approved token?
4. Is the password-eye alternate icon supplied elsewhere, or should the standard visible/hidden icon pair be treated as implementation detail?
