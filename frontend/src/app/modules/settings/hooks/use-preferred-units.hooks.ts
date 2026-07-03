import { PreferredUnits } from '~/api/generated'
import { useGetUnitPreferenceQuery } from '~/api/settings.api'

/**
 * Reads the runner's persisted distance-unit preference for **display**.
 *
 * Falls back to {@link PreferredUnits.Kilometers} while the query is loading,
 * uninitialized, or errored, so every render site receives a concrete unit and
 * never has to branch on an undefined preference. This is a display concern
 * only — storage, the wire, and the plan-gen prompt stay km-native, and the LLM
 * performs zero unit conversion (DEC-086). The single home for the read so
 * plan / history / adaptation render sites resolve the preference the same way.
 */
export const usePreferredUnits = (): PreferredUnits => {
  const { data } = useGetUnitPreferenceQuery(undefined)
  return data?.preferredUnits ?? PreferredUnits.Kilometers
}
