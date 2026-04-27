import type { ComponentType } from 'react'
import { SuggestedInputType } from '~/modules/onboarding/models/onboarding.model'
import { DateTurnInput } from './date-turn-input.component'
import type { InputProps } from './input-for-topic.types'
import { MultiSelectTurnInput } from './multi-select-turn-input.component'
import { NumericTurnInput } from './numeric-turn-input.component'
import { SingleSelectTurnInput } from './single-select-turn-input.component'
import { TextTurnInput } from './text-turn-input.component'

// Component map per spec § Unit 3 R03.4 — discriminator is the integer
// `suggestedInputType` from the server response, so the map stays a
// `Record<SuggestedInputType, ...>` keyed on the underlying numeric values.
// Slice 4's open-conversation surface inherits this exact shape with a
// different map.
export const InputForTopicMap: Record<SuggestedInputType, ComponentType<InputProps>> = {
  [SuggestedInputType.Text]: TextTurnInput,
  [SuggestedInputType.SingleSelect]: SingleSelectTurnInput,
  [SuggestedInputType.MultiSelect]: MultiSelectTurnInput,
  [SuggestedInputType.Numeric]: NumericTurnInput,
  [SuggestedInputType.Date]: DateTurnInput,
}
