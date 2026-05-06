import type {
  OnboardingTopic,
  SuggestedInputType,
} from '~/modules/onboarding/models/onboarding.model'

// Shape submitted by every per-topic input component. Strings on the wire
// because the chat surface re-uses one POST endpoint
// (`/api/v1/onboarding/turns`) that accepts free-text and the backend
// extracts structure from the message via the LLM. The richer "normalized
// answer" path lives behind `/api/v1/onboarding/answers/revise` and is not
// the primary submit path.
export interface InputSubmissionPayload {
  text: string
}

export interface InputOption {
  value: string
  label: string
}

export interface InputProps {
  // Current topic the runner is answering — provided so per-topic
  // components can tailor copy (e.g., "name your race"). The dispatcher
  // accepts `null` for the post-completion idle state but in normal flow
  // an Ask turn always has a topic.
  topic: OnboardingTopic | null
  // Submit handler. The chat surface awaits this; per-topic components are
  // responsible for disabling controls until it resolves so the optimistic
  // UI can flip to `pending`.
  onSubmit: (payload: InputSubmissionPayload) => Promise<void> | void
  // True while a server round-trip is in flight. Disables submit + clears
  // the optional in-flight indicator.
  isSubmitting?: boolean
  // Server-supplied option list for `single-select` / `multi-select` inputs
  // when an Ask turn carries one. Slice 1 only ships PrimaryGoal canned
  // options client-side; downstream Ask turns may attach options as the
  // server learns the user's flow. Other input types ignore this prop.
  options?: readonly InputOption[]
}

export interface InputForTopicProps extends InputProps {
  suggestedInputType: SuggestedInputType
}
