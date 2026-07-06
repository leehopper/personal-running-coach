import { PreferredUnits } from '~/api/generated'

// Shared km/miles option list + boundary parser for every `RadioGroup`-over-
// `PreferredUnits` control (the Settings units toggle and the onboarding
// units-first field). One source of truth so the two controls can never drift.

/** Selectable units in display order. `value` is the wire enum; `label` is the copy. */
export const UNIT_OPTIONS: ReadonlyArray<{ value: PreferredUnits; label: string }> = [
  { value: PreferredUnits.Kilometers, label: 'Kilometers' },
  { value: PreferredUnits.Miles, label: 'Miles' },
]

/**
 * Resolves a raw Radix `RadioGroup` string value back to a known
 * {@link PreferredUnits} by matching an option (never casting an unvalidated
 * `Number()` into the `0 | 1` union). An unrecognized value falls back to
 * {@link PreferredUnits.Kilometers}.
 */
export const parsePreferredUnits = (value: string): PreferredUnits =>
  UNIT_OPTIONS.find((option) => String(option.value) === value)?.value ?? PreferredUnits.Kilometers
