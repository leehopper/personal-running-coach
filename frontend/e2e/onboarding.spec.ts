import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// Onboarding chat e2e per spec § Unit 3 R03.2 / R03.5 / R03.9 / R03.10.
// Strategy:
//   1. Use the real backend for `register` so the auth cookie + antiforgery
//      pair are seeded the same way the runtime app expects (the auth.spec
//      already covers that contract). The `__Host-RunCoach` cookie carries
//      the session straight into the onboarding flow.
//   2. Stub `GET /api/v1/onboarding/state` and `POST /api/v1/onboarding/turns`
//      with deterministic Playwright `route` handlers so the test never
//      touches the real LLM (per task description § "use Playwright `route`
//      interception, not real LLM"). The stubs walk through the canonical
//      six DEC-047 topics with `Ask`-shaped responses 1-5 and a `Complete`
//      response on turn 6 carrying the new `planId`. On completion, the
//      home redirect-guard fetches `/api/v1/onboarding/state` again and is
//      served the `Completed` shape so it lets the page through to `/`.
//   3. After the final `Complete` response, also stub `GET /api/v1/plan/current`
//      so the home page renders without hitting the real plan generator.
//      Slice 1's `HomePage` itself is the smoke target — we just assert the
//      navigation flipped to `/` and the route guard let the user through.

const SESSION_COOKIE = '__Host-RunCoach'
const VALID_PASSWORD = 'Correct-Horse-9!'

// Fresh email per run so the suite is re-runnable against a shared dev
// Postgres without collisions. `npm run e2e:clean` flushes the orphans.
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

// Wire-format integer enums duplicated from
// `frontend/src/app/modules/onboarding/models/onboarding.model.ts` so the
// stubs round-trip the exact JSON the page-level Zod schema accepts.
// Keeping the literals inline (vs. importing the model file) holds the
// e2e suite at arm's length from the implementation under test.
const OnboardingTurnKind = { Ask: 0, Complete: 1 } as const
const OnboardingTopic = {
  PrimaryGoal: 0,
  TargetEvent: 1,
  CurrentFitness: 2,
  WeeklySchedule: 3,
  InjuryHistory: 4,
  Preferences: 5,
} as const
const SuggestedInputType = {
  Text: 0,
  SingleSelect: 1,
  MultiSelect: 2,
  Numeric: 3,
  Date: 4,
} as const
const AnthropicContentBlockType = { Text: 0 } as const
const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const

interface AskTurn {
  topic: number
  suggestedInputType: number
  prompt: string
}

// Canonical six-topic walk. Each entry drives one Ask response from the
// stub and one assertion on the rendered transcript — turn N's ask copy
// becomes the visible assistant message, and the user's answer-method
// (radio / checkbox / numeric / date / textarea) is dispatched off
// `suggestedInputType` exactly as the production `InputForTopic` does.
const ONBOARDING_FLOW: readonly AskTurn[] = [
  {
    topic: OnboardingTopic.TargetEvent,
    suggestedInputType: SuggestedInputType.Date,
    prompt: 'When is your target event?',
  },
  {
    topic: OnboardingTopic.CurrentFitness,
    suggestedInputType: SuggestedInputType.Numeric,
    prompt: 'How many km do you currently run per week?',
  },
  {
    topic: OnboardingTopic.WeeklySchedule,
    suggestedInputType: SuggestedInputType.MultiSelect,
    prompt: 'Which days can you train?',
  },
  {
    topic: OnboardingTopic.InjuryHistory,
    suggestedInputType: SuggestedInputType.Text,
    prompt: 'Any recent injuries we should know about?',
  },
  {
    topic: OnboardingTopic.Preferences,
    suggestedInputType: SuggestedInputType.Text,
    prompt: 'Anything else we should consider?',
  },
] as const

const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234'

const buildAskBody = (turn: AskTurn, completedTopics: number) => ({
  kind: OnboardingTurnKind.Ask,
  assistantBlocks: [{ type: AnthropicContentBlockType.Text, text: turn.prompt }],
  topic: turn.topic,
  suggestedInputType: turn.suggestedInputType,
  progress: { completedTopics, totalTopics: 6 },
  planId: null,
})

const buildCompleteBody = () => ({
  kind: OnboardingTurnKind.Complete,
  assistantBlocks: [
    {
      type: AnthropicContentBlockType.Text,
      text: 'All set — your plan is ready.',
    },
  ],
  topic: null,
  suggestedInputType: null,
  progress: { completedTopics: 6, totalTopics: 6 },
  planId: completedPlanId,
})

interface StubState {
  // Number of POSTs to /onboarding/turns served so far. Drives both the
  // wire-format `progress.completedTopics` value and the discriminator
  // (Ask for turns 1-5; Complete for turn 6).
  turnsServed: number
  // The topic the assistant most recently asked about. Read by the
  // mid-flow `GET /onboarding/state` stub so the page's
  // `transcriptReplaced` effect picks the right input hint.
  currentTopic: number | null
  // Idempotency keys observed on POST. Asserts the page mints a fresh
  // UUID for every distinct submission and reuses keys on retry.
  idempotencyKeys: string[]
  // Whether onboarding has crossed the Complete boundary. Drives the
  // shape of `GET /onboarding/state` so the post-completion home guard
  // lets the page through to `/`.
  isComplete: boolean
}

const installOnboardingStubs = async (page: Page, state: StubState): Promise<void> => {
  // GET /api/v1/onboarding/state — the page-level mount-effect calls this
  // immediately after `<RequireAuth>` settles. Pre-stream we serve a 404
  // (≡ "no stream yet — start fresh"). Once any POST has landed, the
  // stub flips to a 200 InProgress shape so the page-level effect does
  // NOT call `transcriptCleared()` on the next refetch (the `submitOnboardingTurn`
  // mutation invalidates the `Onboarding` tag, which triggers a refetch
  // mid-flow). Post-completion we serve `isComplete: true` so the home
  // redirect-guard lets `/` render.
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    if (state.isComplete) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: '00000000-0000-0000-0000-000000000001',
          status: OnboardingStatus.Completed,
          currentTopic: null,
          completedTopics: 6,
          totalTopics: 6,
          isComplete: true,
          outstandingClarifications: [],
          primaryGoal: null,
          targetEvent: null,
          currentFitness: null,
          weeklySchedule: null,
          injuryHistory: null,
          preferences: null,
          currentPlanId: completedPlanId,
        }),
      })
      return
    }
    if (state.turnsServed === 0) {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({
          type: 'about:blank',
          title: 'Not Found',
          status: 404,
          detail: 'No onboarding stream for this user yet.',
        }),
      })
      return
    }
    // Mid-flow refetch: the stream exists but onboarding is incomplete.
    // The current topic / completedTopics counter mirror what the
    // most-recent POST returned so the slice's replay action paints the
    // same six-segment progress the chat already shows.
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: '00000000-0000-0000-0000-000000000001',
        status: OnboardingStatus.InProgress,
        currentTopic: state.currentTopic,
        completedTopics: state.turnsServed,
        totalTopics: 6,
        isComplete: false,
        outstandingClarifications: [],
        primaryGoal: null,
        targetEvent: null,
        currentFitness: null,
        weeklySchedule: null,
        injuryHistory: null,
        preferences: null,
        currentPlanId: null,
      }),
    })
  })

  // POST /api/v1/onboarding/turns — six turns: five Ask responses then a
  // Complete. The handler captures the idempotency key off each request so
  // the test can later assert distinct keys per submission (and equal keys
  // on a retry, were the test exercising one).
  await page.route('**/api/v1/onboarding/turns', async (route: Route) => {
    const body = (await route.request().postDataJSON()) as {
      idempotencyKey?: string
      text?: string
    }
    if (typeof body.idempotencyKey === 'string') {
      state.idempotencyKeys.push(body.idempotencyKey)
    }

    const upcomingTurnIndex = state.turnsServed
    state.turnsServed += 1

    // Server saw the user's nth answer → completedTopics now equals n.
    const completedAfterThisTurn = upcomingTurnIndex + 1

    if (completedAfterThisTurn >= 6) {
      state.isComplete = true
      state.currentTopic = null
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(buildCompleteBody()),
      })
      return
    }

    // Pick the next Ask: ONBOARDING_FLOW[i] is the question that gets
    // posed AFTER the user has answered i+1 topics. (The first Ask the
    // page renders is fully client-side from the canned PrimaryGoal
    // fallback in `single-select-turn-input.component.tsx`; the very
    // first server response asks about TargetEvent.)
    const nextAsk = ONBOARDING_FLOW[upcomingTurnIndex]
    state.currentTopic = nextAsk.topic
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildAskBody(nextAsk, completedAfterThisTurn)),
    })
  })

  // GET /api/v1/plan/current — the post-onboarding home guard fetches
  // this once it lets the page through. Stub a minimal shape so the home
  // page does not error out; the e2e target here is the navigation, not
  // the plan rendering itself (Slice 1 Unit 4 covers that).
  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        planId: completedPlanId,
        userId: '00000000-0000-0000-0000-000000000001',
        generatedAt: new Date().toISOString(),
        previousPlanId: null,
        macroPhases: [],
        mesoWeeks: [],
        microWorkouts: [],
      }),
    })
  })
}

const submitTextInput = async (page: Page, value: string): Promise<void> => {
  await page.getByTestId('text-turn-input-field').fill(value)
  await page.getByRole('button', { name: /send/i }).click()
}

test('register → complete six-topic onboarding → navigate to /', async ({ page }) => {
  const email = uniqueEmail()
  const stubState: StubState = {
    turnsServed: 0,
    currentTopic: null,
    idempotencyKeys: [],
    isComplete: false,
  }
  await installOnboardingStubs(page, stubState)

  // 1. Register a fresh user → real backend issues the session cookie. The
  //    register page chains login automatically; on success the SPA tries
  //    to land on `/`, the home redirect-guard sees the stubbed 404, and
  //    bounces to `/onboarding`.
  await page.goto('/register')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/onboarding')

  // 2. The page should render the canned PrimaryGoal single-select. The
  //    initial assistant copy is empty until the first server Ask lands —
  //    the radio fallback list is what proves the input dispatcher is
  //    wired correctly to the canonical first topic.
  await expect(page.getByTestId('onboarding-chat')).toBeVisible()
  await expect(page.getByTestId('single-select-turn-input')).toBeVisible()

  // Topic 1 — PrimaryGoal: pick the first radio and submit.
  await page.locator('input[type="radio"]').first().check()
  await page.getByRole('button', { name: /send/i }).click()

  // After the first server Ask (TargetEvent / date input) lands, the
  // input dispatcher swaps to a date picker and the progress indicator
  // shows one completed segment. The `data-state` attribute lives on
  // each `<li>` itself (not a descendant), so `locator(...)` would miss
  // — use a CSS attribute selector against the list directly.
  await expect(page.getByTestId('date-turn-input')).toBeVisible()
  await expect(
    page.getByTestId('topic-progress-indicator').locator('li[data-state="completed"]'),
  ).toHaveCount(1)

  // Topic 2 — TargetEvent: pick a date six months out.
  const futureDate = new Date()
  futureDate.setMonth(futureDate.getMonth() + 6)
  const isoDate = futureDate.toISOString().slice(0, 10)
  await page.getByTestId('date-turn-input-field').fill(isoDate)
  await page.getByRole('button', { name: /send/i }).click()

  // Topic 3 — CurrentFitness: numeric input.
  await expect(page.getByTestId('numeric-turn-input')).toBeVisible()
  await page.getByTestId('numeric-turn-input-field').fill('30')
  await page.getByRole('button', { name: /send/i }).click()

  // Topic 4 — WeeklySchedule: multi-select. Pick two days.
  await expect(page.getByTestId('multi-select-turn-input')).toBeVisible()
  const checkboxes = page.getByTestId('multi-select-turn-input').locator('input[type="checkbox"]')
  await checkboxes.nth(0).check()
  await checkboxes.nth(2).check()
  await page.getByRole('button', { name: /send/i }).click()

  // Topic 5 — InjuryHistory: free-text.
  await expect(page.getByTestId('text-turn-input')).toBeVisible()
  await submitTextInput(page, 'No current injuries.')

  // Topic 6 — Preferences: free-text. This is the final turn — the next
  // POST returns Complete, which navigates the page to `/`.
  await expect(page.getByTestId('text-turn-input')).toBeVisible()
  await submitTextInput(page, 'Prefer mornings.')

  // 3. Final assertion: navigation to `/` succeeded. The post-completion
  //    state stub flips `isComplete` true so the home redirect-guard lets
  //    the route through, and the plan-current stub keeps the page from
  //    erroring out while it waits for the (stubbed) plan payload.
  await expect(page).toHaveURL('/')

  // 4. Sanity-check the wire contract that the chat surface upheld:
  //    every POST carried a UUID-shaped idempotency key, and there were
  //    exactly six distinct keys (one per topic, no retries in the happy
  //    path so the keys must all differ).
  expect(stubState.idempotencyKeys).toHaveLength(6)
  const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  for (const key of stubState.idempotencyKeys) {
    expect(key).toMatch(uuidPattern)
  }
  expect(new Set(stubState.idempotencyKeys).size).toBe(6)

  // The session cookie is still present — the onboarding flow did not
  // tear it down. Belt-and-suspenders against a regression that logs the
  // user out mid-flow.
  const cookies = await page.context().cookies()
  expect(cookies.find((cookie) => cookie.name === SESSION_COOKIE)).toBeDefined()
})
