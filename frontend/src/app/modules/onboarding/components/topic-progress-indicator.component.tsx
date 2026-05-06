import type { ReactElement } from 'react'
import type { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'
import {
  DEFAULT_TOPIC_ORDER,
  SEGMENT_STYLES,
  TOPIC_LABELS,
  stateForTopic,
} from './topic-progress-indicator.helpers'

export interface TopicProgressIndicatorProps {
  // Topics already captured (in canonical DEC-047 order).
  completedTopics: readonly OnboardingTopic[]
  // The topic the runner is currently answering. Null when onboarding is
  // complete (every segment is `completed`).
  currentTopic: OnboardingTopic | null
  // Optional override for the segment ordering. Defaults to the canonical
  // DEC-047 order. Exposed so a future flow that skips `TargetEvent` for
  // non-race goals can pass a five-segment array without forking the
  // component.
  topics?: readonly OnboardingTopic[]
  className?: string
}

/**
 * Six-segment progress indicator (one chip per DEC-047 onboarding topic)
 * with completed / current / pending states. Segments transition between
 * states via a 200ms ease-out CSS animation so colour shifts feel intentional
 * without dragging in a runtime animation library for what is otherwise a
 * static layout.
 */
export const TopicProgressIndicator = ({
  completedTopics,
  currentTopic,
  topics = DEFAULT_TOPIC_ORDER,
  className,
}: TopicProgressIndicatorProps): ReactElement => (
  <ol
    aria-label="Onboarding progress"
    data-testid="topic-progress-indicator"
    className={`flex w-full items-center gap-2 ${className ?? ''}`}
  >
    {topics.map((topic) => {
      const state = stateForTopic(topic, completedTopics, currentTopic)
      return (
        <li
          // Topic ids are stable over the lifetime of the indicator, so the
          // numeric topic value is a safe key (no array-index sin).
          key={topic}
          aria-current={state === 'current' ? 'step' : undefined}
          data-state={state}
          data-topic={topic}
          className={`flex flex-1 items-center justify-center rounded-full border px-3 py-1 text-xs font-medium transition-colors duration-200 ease-out ${SEGMENT_STYLES[state]}`}
        >
          <span className="truncate">{TOPIC_LABELS[topic]}</span>
        </li>
      )
    })}
  </ol>
)
