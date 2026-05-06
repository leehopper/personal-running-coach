import { describe, expect, it } from 'vitest'
import {
  AnthropicContentBlockType,
  OnboardingTopic,
  OnboardingTurnKind,
  SuggestedInputType,
} from '~/modules/onboarding/models/onboarding.model'
import { onboardingTurnResponseSchema } from './onboarding-turn-response.schema'

// The schema is the runtime contract between the chat surface and the
// backend's POST /api/v1/onboarding/turns response. These tests pin the
// three discriminator branches plus a representative slice of edge cases
// the wire format must reject. They are deliberately format-aware (camelCase
// keys, integer enums) — a backend renaming or adding a JsonStringEnumConverter
// would surface here as a fixture failure.

const validAskPayload = {
  kind: OnboardingTurnKind.Ask,
  assistantBlocks: [
    { type: AnthropicContentBlockType.Text, text: 'Welcome — what is your primary goal?' },
  ],
  topic: OnboardingTopic.PrimaryGoal,
  suggestedInputType: SuggestedInputType.SingleSelect,
  progress: { completedTopics: 0, totalTopics: 6 },
  planId: null,
}

const validCompletePayload = {
  kind: OnboardingTurnKind.Complete,
  assistantBlocks: [
    { type: AnthropicContentBlockType.Text, text: 'Plan ready — taking you to your dashboard.' },
  ],
  topic: null,
  suggestedInputType: null,
  progress: { completedTopics: 6, totalTopics: 6 },
  planId: '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234',
}

const validErrorPayload = {
  kind: OnboardingTurnKind.Error,
  message: 'Server response failed schema validation.',
}

describe('onboardingTurnResponseSchema', () => {
  it('parses a valid Ask payload', () => {
    const actual = onboardingTurnResponseSchema.parse(validAskPayload)

    expect(actual.kind).toBe(OnboardingTurnKind.Ask)
    if (actual.kind === OnboardingTurnKind.Ask) {
      expect(actual.topic).toBe(OnboardingTopic.PrimaryGoal)
      expect(actual.suggestedInputType).toBe(SuggestedInputType.SingleSelect)
      expect(actual.assistantBlocks).toHaveLength(1)
      expect(actual.planId).toBeNull()
    }
  })

  it('parses a valid Complete payload', () => {
    const actual = onboardingTurnResponseSchema.parse(validCompletePayload)

    expect(actual.kind).toBe(OnboardingTurnKind.Complete)
    if (actual.kind === OnboardingTurnKind.Complete) {
      expect(actual.planId).toBe('8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234')
      expect(actual.topic).toBeNull()
      expect(actual.suggestedInputType).toBeNull()
    }
  })

  it('parses a synthetic Error payload (client-side variant)', () => {
    const actual = onboardingTurnResponseSchema.parse(validErrorPayload)

    expect(actual.kind).toBe(OnboardingTurnKind.Error)
    if (actual.kind === OnboardingTurnKind.Error) {
      expect(actual.message).toBe('Server response failed schema validation.')
    }
  })

  it('parses every Thinking-block pass-through opaquely', () => {
    const payload = {
      ...validAskPayload,
      assistantBlocks: [
        { type: AnthropicContentBlockType.Thinking, text: '' },
        { type: AnthropicContentBlockType.Text, text: 'visible reply' },
      ],
    }

    const actual = onboardingTurnResponseSchema.parse(payload)

    expect(actual.kind).toBe(OnboardingTurnKind.Ask)
    if (actual.kind === OnboardingTurnKind.Ask) {
      expect(actual.assistantBlocks).toHaveLength(2)
      expect(actual.assistantBlocks[0].type).toBe(AnthropicContentBlockType.Thinking)
    }
  })

  it('rejects an Ask payload missing the topic field', () => {
    const payloadWithoutTopic: Record<string, unknown> = { ...validAskPayload }
    delete payloadWithoutTopic.topic

    const actual = onboardingTurnResponseSchema.safeParse(payloadWithoutTopic)

    expect(actual.success).toBe(false)
  })

  it('rejects an Ask payload with a non-null planId', () => {
    const payload = {
      ...validAskPayload,
      planId: '00000000-0000-0000-0000-000000000002',
    }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects a Complete payload with a missing planId', () => {
    const payloadWithoutPlanId: Record<string, unknown> = {
      ...validCompletePayload,
    }
    delete payloadWithoutPlanId.planId

    const actual = onboardingTurnResponseSchema.safeParse(payloadWithoutPlanId)

    expect(actual.success).toBe(false)
  })

  it('rejects a Complete payload with a non-UUID planId', () => {
    const payload = { ...validCompletePayload, planId: 'not-a-guid' }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects an unknown discriminator', () => {
    const payload = { ...validAskPayload, kind: 99 }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects an unknown topic integer', () => {
    const payload = { ...validAskPayload, topic: 99 }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects an unknown suggestedInputType integer', () => {
    const payload = { ...validAskPayload, suggestedInputType: 99 }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects progress with completedTopics out of range', () => {
    const payload = {
      ...validAskPayload,
      progress: { completedTopics: 7, totalTopics: 6 },
    }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })

  it('rejects an assistant block with an unknown block type', () => {
    const payload = {
      ...validAskPayload,
      assistantBlocks: [{ type: 99, text: '' }],
    }

    const actual = onboardingTurnResponseSchema.safeParse(payload)

    expect(actual.success).toBe(false)
  })
})
