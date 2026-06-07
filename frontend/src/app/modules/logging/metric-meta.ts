/**
 * Frontend metric-meta map (DEC-072 / DEC-075). The single place UI labels +
 * units for the open `WorkoutLog.metrics` bag live, so the log form (PR6b) and
 * the history list (PR7) render identical wording and cannot drift.
 *
 * Keys are the canonical lower-camel wire keys from the backend
 * `RunCoach.Api.Modules.Training.Constants.WorkoutMetricKeys` — they ARE the
 * contract, so they mirror the backend verbatim. Labels here are UI-facing
 * (e.g. `hrAvg → "Avg HR"`) and are intentionally friendlier than the compact
 * labels the backend `WorkoutMetricKeys.Metadata` uses for LLM one-liners
 * (`"HR"`); the two maps share the same key set, not the same label strings.
 */
export interface WorkoutMetricMeta {
  /** UI-facing label (e.g. "Avg HR"). */
  label: string
  /** Display unit suffix, or `''` for unitless metrics (e.g. RPE). */
  unit: string
}

export const WORKOUT_METRIC_META = {
  rpe: { label: 'RPE', unit: '' },
  hrAvg: { label: 'Avg HR', unit: 'bpm' },
  hrMax: { label: 'Max HR', unit: 'bpm' },
  calories: { label: 'Calories', unit: 'kcal' },
  hrv: { label: 'HRV', unit: 'ms' },
  sleepScore: { label: 'Sleep score', unit: '' },
  recoveryScore: { label: 'Recovery score', unit: '' },
  cadence: { label: 'Cadence', unit: 'spm' },
  elevationGain: { label: 'Elevation gain', unit: 'm' },
  power: { label: 'Power', unit: 'W' },
  weather: { label: 'Weather', unit: '' },
  terrain: { label: 'Terrain', unit: '' },
} as const satisfies Record<string, WorkoutMetricMeta>

/** Canonical metric key (lower-camel wire key). */
export type WorkoutMetricKey = keyof typeof WORKOUT_METRIC_META

/**
 * The scalar numeric metrics surfaced as optional fields in the `/log` form's
 * "More details" section at MVP-0 — the self-reportable effort + intensity
 * signals. Device-sourced metrics (cadence, power, hrv, sleep/recovery) and the
 * free-text contextual ones (weather, terrain) are accepted by the data model
 * but not yet form fields; they bolt on without a migration (DEC-072).
 */
export const FORM_METRIC_KEYS = [
  'rpe',
  'hrAvg',
  'hrMax',
  'elevationGain',
] as const satisfies readonly WorkoutMetricKey[]

export type FormMetricKey = (typeof FORM_METRIC_KEYS)[number]

/** Composes the display label with its unit suffix, e.g. "Avg HR (bpm)". */
export const metricFieldLabel = (key: WorkoutMetricKey): string => {
  const { label, unit } = WORKOUT_METRIC_META[key]
  return unit.length > 0 ? `${label} (${unit})` : label
}
