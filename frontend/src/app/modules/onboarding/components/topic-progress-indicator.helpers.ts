import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'

// Per-segment lifecycle. The current segment is highlighted; completed
// segments take a filled "done" styling; pending segments stay dim.
export type TopicSegmentState = 'completed' | 'current' | 'pending'

// Canonical six-segment order per DEC-047 / spec § Design Considerations
// "Topic ordering". Re-exported as a value so consumers can splice it (the
// `TargetEvent`-skipped variant is the only foreseen variant in Slice 1).
export const DEFAULT_TOPIC_ORDER: readonly OnboardingTopic[] = [
  OnboardingTopic.PrimaryGoal,
  OnboardingTopic.TargetEvent,
  OnboardingTopic.CurrentFitness,
  OnboardingTopic.WeeklySchedule,
  OnboardingTopic.InjuryHistory,
  OnboardingTopic.Preferences,
] as const

// Human-readable label per topic. Pulled into a static map so the component
// stays render-pure and so the labels are easy to scan during reviews.
// Trademark-clean phrasing — none of these strings reference VDOT.
export const TOPIC_LABELS: Record<OnboardingTopic, string> = {
  [OnboardingTopic.PrimaryGoal]: 'Goal',
  [OnboardingTopic.TargetEvent]: 'Event',
  [OnboardingTopic.CurrentFitness]: 'Fitness',
  [OnboardingTopic.WeeklySchedule]: 'Schedule',
  [OnboardingTopic.InjuryHistory]: 'Injuries',
  [OnboardingTopic.Preferences]: 'Preferences',
}

// Tailwind class strings per state. Transitions deliver the 200ms ease-out
// animation called out in spec § Unit 3 R03.5 — `motion/react` is reserved
// for richer animations elsewhere; segment colour shifts are simple enough
// that a CSS transition is the lighter-weight implementation.
export const SEGMENT_STYLES: Record<TopicSegmentState, string> = {
  completed: 'bg-slate-900 text-slate-50 border-slate-900',
  current: 'bg-slate-50 text-slate-900 border-slate-900 ring-2 ring-slate-900/20',
  pending: 'bg-slate-50 text-slate-500 border-slate-200',
}

export const stateForTopic = (
  topic: OnboardingTopic,
  completedTopics: readonly OnboardingTopic[],
  currentTopic: OnboardingTopic | null,
): TopicSegmentState => {
  if (completedTopics.includes(topic)) {
    return 'completed'
  }
  if (currentTopic !== null && currentTopic === topic) {
    return 'current'
  }
  return 'pending'
}
