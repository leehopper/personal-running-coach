import { randomUUID } from 'node:crypto'
import { expect, test, type Page, type Route } from '@playwright/test'

// End-to-end coverage for the streaming conversation UX.
//
// Strategy:
//   1. `register` hits the REAL backend so the session cookie + antiforgery
//      pair are seeded exactly as the runtime app expects (the hand-rolled SSE
//      POST reads the `__Host-Xsrf-Request` cookie and sets `X-XSRF-TOKEN`).
//   2. Onboarding state + plan/current are stubbed so the home page renders the
//      plan view; the interactive coach chat lives on its own `/coach` route
//      (SPLIT/Alpine Slice 1), reached via the shell's TabBar COACH tab.
//   3. The conversation endpoints are stubbed at the wire: `GET /conversation/
//      timeline`, the SSE `POST /conversation/messages`, and the JSON
//      `POST /conversation/logs/confirm`.
//
// NOTE on streaming: Playwright's `route.fulfill` delivers the whole SSE body in
// one shot — it does not stream incrementally — so token-by-token paint is
// covered by the Vitest hook spec, not here. This E2E asserts the
// composer → answer and composer → confirmation-card → Confirm flows.

// eslint-disable-next-line sonarjs/no-hardcoded-passwords -- static E2E fixture password (matches the sibling e2e specs), not a real credential
const VALID_PASSWORD = 'Correct-Horse-9!'
const uniqueEmail = (): string => `e2e-${randomUUID()}@runcoach.test`

const OnboardingStatus = { NotStarted: 0, InProgress: 1, Completed: 2 } as const
const completedPlanId = '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b9999'
const userId = '00000000-0000-0000-0000-000000000042'

const frame = (event: string, data: unknown): string =>
  `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`
const sse = (...frames: string[]): string => `: hb\n\n${frames.join('')}`

const buildPlanProjection = () => {
  const dayKeys = [
    'sunday',
    'monday',
    'tuesday',
    'wednesday',
    'thursday',
    'friday',
    'saturday',
  ] as const
  const runSlot = { slotType: 'Run', workoutType: 'Easy', notes: '' }
  const buildWorkout = (dayOfWeek: number, title: string) => ({
    dayOfWeek,
    workoutType: 'Easy',
    title,
    targetDistanceKm: 8,
    targetDurationMinutes: 48,
    targetPaceEasySecPerKm: 360,
    targetPaceFastSecPerKm: 330,
    segments: [],
    warmupNotes: '',
    cooldownNotes: '',
    coachingNotes: 'Hold pace-zone index easy throughout.',
    perceivedEffort: 4,
  })
  return {
    planId: completedPlanId,
    userId,
    planStartDate: '2026-06-07',
    generatedAt: '2026-06-07T12:00:00.000Z',
    previousPlanId: null,
    promptVersion: 'plan-generation-v1',
    modelId: 'claude-sonnet-4-6',
    macro: {
      totalWeeks: 12,
      goalDescription: 'Build aerobic base for a half marathon.',
      rationale: 'Daniels-Gilbert zones drive the pace targets across phases.',
      warnings: '',
      phases: [
        {
          phaseType: 'Base',
          weeks: 12,
          weeklyDistanceStartKm: 30,
          weeklyDistanceEndKm: 40,
          intensityDistribution: '80/20',
          allowedWorkoutTypes: ['Easy', 'LongRun', 'Recovery'],
          targetPaceEasySecPerKm: 360,
          targetPaceFastSecPerKm: 330,
          notes: 'Aerobic foundation — pace-zone index easy.',
          includesDeload: false,
        },
      ],
    },
    mesoWeeks: [
      {
        weekNumber: 1,
        phaseType: 'Base',
        weeklyTargetKm: 30,
        isDeloadWeek: false,
        weekSummary: 'Week 1 — pace-zone index easy mileage.',
        ...Object.fromEntries(dayKeys.map((key) => [key, runSlot])),
      },
    ],
    microWorkoutsByWeek: {
      1: { workouts: [buildWorkout(1, 'Monday easy run')] },
    },
  }
}

const ackTurn = {
  kind: 1,
  turnId: 'ack-1',
  createdAt: '2026-06-29T10:05:00Z',
  interactive: { content: 'Logged — solid work.', isErrored: false },
  proactive: null,
}

interface StubState {
  messageCalls: number
  confirmed: boolean
}

const installStubs = async (page: Page, state: StubState): Promise<void> => {
  await page.route('**/api/v1/onboarding/state', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId,
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
  })

  await page.route('**/api/v1/plan/current', async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildPlanProjection()),
    })
  })

  await page.route('**/api/v1/conversation/timeline', async (route: Route) => {
    // After a successful confirm the Conversation tag is invalidated and this
    // refetches — surface the coach ack turn then.
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ turns: state.confirmed ? [ackTurn] : [] }),
    })
  })

  await page.route('**/api/v1/conversation/messages', async (route: Route) => {
    state.messageCalls += 1
    // First message: a question → streamed answer + done. Second: a workout
    // description → confirmation card (no done frame).
    const body =
      state.messageCalls === 1
        ? sse(
            frame('token', { delta: 'Your easy run ' }),
            frame('token', { delta: 'looked solid.' }),
            frame('done', { turnId: 'coach-1' }),
          )
        : sse(
            frame('card', {
              draft: {
                occurredOn: '2026-06-29',
                distanceValue: 5,
                distanceUnit: 0,
                durationHours: 0,
                durationMinutes: 25,
                durationSeconds: 0,
                completionStatus: 0,
                notes: null,
              },
              prescription: null,
            }),
          )
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      body,
    })
  })

  await page.route('**/api/v1/conversation/logs/confirm', async (route: Route) => {
    state.confirmed = true
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        workoutLogId: '11111111-1111-4111-8111-111111111111',
        adaptation: {
          kind: 0,
          adaptationKind: 0,
          errorMessage: null,
          retryable: false,
          retryAfterSeconds: null,
        },
      }),
    })
  })
}

const ask = async (page: Page, message: string): Promise<void> => {
  await page.getByLabel(/message your coach/i).fill(message)
  await page.getByRole('button', { name: /send/i }).click()
}

test('register → ask a question → describe a workout → confirm the parsed log', async ({
  page,
}) => {
  const state: StubState = { messageCalls: 0, confirmed: false }
  await installStubs(page, state)

  await page.goto('/register')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(VALID_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL('/')

  // Coach chat lives on its own /coach route — reach it via the TabBar.
  await page.getByTestId('tab-coach').click()
  await expect(page).toHaveURL('/coach')
  await expect(page.getByTestId('coach-chat')).toBeVisible()

  // 1. Ask a question → the streamed answer renders and persists in the timeline.
  await ask(page, 'how was my run?')
  await expect(page.getByText('Your easy run looked solid.')).toBeVisible()

  // 2. Describe a workout → the confirmation card renders the parsed draft.
  await ask(page, 'logged 5k in 25 minutes this morning')
  const card = page.getByTestId('log-confirmation-card')
  await expect(card).toBeVisible()
  await expect(card.getByText(/5 km/i)).toBeVisible()
  await expect(card.getByText('25:00')).toBeVisible()

  // 3. Confirm → the card dismisses and the coach ack turn appears (timeline refetch).
  await card.getByRole('button', { name: /^confirm$/i }).click()
  await expect(card).toBeHidden()
  await expect(page.getByText('Logged — solid work.')).toBeVisible()

  // Trademark guard — no "VDOT" anywhere in the rendered DOM.
  const bodyHtml = await page.locator('body').innerHTML()
  expect(bodyHtml.match(/vdot/gi) ?? []).toHaveLength(0)
})
