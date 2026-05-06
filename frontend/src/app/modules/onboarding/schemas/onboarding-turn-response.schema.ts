import { z } from 'zod'
import {
  AnthropicContentBlockType,
  OnboardingTopic,
  OnboardingTurnKind,
  SuggestedInputType,
} from '~/modules/onboarding/models/onboarding.model'

// Discriminated-union Zod schema for `OnboardingTurnResponse` per Slice 1
// § Frontend chat UX (R-065). Discriminator is `kind`, an integer that
// mirrors the backend `OnboardingTurnKind` enum (no JsonStringEnumConverter
// is registered on the controller, so the wire value is the underlying
// integer — see `models/onboarding.model.ts` for the enum-value contract).
//
// Why a literal-set check on enums rather than `z.nativeEnum`: the model
// layer exports each enum as a `const` object + value-type alias, which
// keeps tree-shaking happy and avoids dragging the TypeScript-style enum
// runtime into the bundle. The consequence here is that we have to spell
// out the accepted integers explicitly. The pattern is `z.union([z.literal(0), …])`.

// Anthropic content block (closed-shape Pattern B record).
const anthropicContentBlockSchema = z.object({
  type: z.union([
    z.literal(AnthropicContentBlockType.Text),
    z.literal(AnthropicContentBlockType.Thinking),
  ]),
  text: z.string(),
})

// `topic` accepts every `OnboardingTopic` integer value. Listing them is
// cheaper than `z.nativeEnum` (which would force the model file to expose
// a TypeScript enum for runtime introspection).
const onboardingTopicSchema = z.union([
  z.literal(OnboardingTopic.PrimaryGoal),
  z.literal(OnboardingTopic.TargetEvent),
  z.literal(OnboardingTopic.CurrentFitness),
  z.literal(OnboardingTopic.WeeklySchedule),
  z.literal(OnboardingTopic.InjuryHistory),
  z.literal(OnboardingTopic.Preferences),
])

const suggestedInputTypeSchema = z.union([
  z.literal(SuggestedInputType.Text),
  z.literal(SuggestedInputType.SingleSelect),
  z.literal(SuggestedInputType.MultiSelect),
  z.literal(SuggestedInputType.Numeric),
  z.literal(SuggestedInputType.Date),
])

const progressSchema = z.object({
  completedTopics: z.number().int().min(0).max(6),
  totalTopics: z.number().int().min(5).max(6),
})

// `assistantBlocks` arrives from the backend as a raw `JsonDocument` array;
// keep it as a plain array on the wire so non-text block types
// (`thinking`, future `tool_use`) round-trip without a schema bump.
const assistantBlocksSchema = z.array(anthropicContentBlockSchema)

// Variant 1 — server asked another question. `topic`, `suggestedInputType`,
// and a non-null `planId === null` are required by the backend's response
// shape; the `planId` is left in the schema for shape parity with the
// completion variant.
const askResponseSchema = z.object({
  kind: z.literal(OnboardingTurnKind.Ask),
  assistantBlocks: assistantBlocksSchema,
  topic: onboardingTopicSchema,
  suggestedInputType: suggestedInputTypeSchema,
  progress: progressSchema,
  planId: z.null(),
})

// Variant 2 — onboarding finished, a plan was generated. The runner-visible
// chat UX shows the assistant's farewell blocks while the page navigates
// to the plan view.
const completeResponseSchema = z.object({
  kind: z.literal(OnboardingTurnKind.Complete),
  assistantBlocks: assistantBlocksSchema,
  topic: z.null(),
  suggestedInputType: z.null(),
  progress: progressSchema,
  planId: z.uuid(),
})

// Variant 3 — synthetic client-side error. The backend never emits
// `kind: -1`; this shape exists so the chat UI can carry a parse / network
// failure through the same discriminated-union dispatch the success path
// uses, avoiding a separate error-state surface.
const errorResponseSchema = z.object({
  kind: z.literal(OnboardingTurnKind.Error),
  message: z.string().min(1),
})

/**
 * Discriminated-union schema for the POST /api/v1/onboarding/turns response.
 * Use `parse` (throws) or `safeParse` (returns a tagged result) — both are
 * exhaustive over the `kind` discriminator.
 */
export const onboardingTurnResponseSchema = z.discriminatedUnion('kind', [
  askResponseSchema,
  completeResponseSchema,
  errorResponseSchema,
])

export type OnboardingTurnResponseFromSchema = z.infer<typeof onboardingTurnResponseSchema>
