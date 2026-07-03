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
 *
 * NOT for the write path: the km fallback collapses "loading", "errored", and
 * "genuinely km" into one value, so a miles-preferring runner submitting on a
 * cold cache would have their input silently interpreted as km. The log form's
 * numeric write uses {@link usePreferredUnitsResolution} and defers until the
 * preference is actually resolved.
 */
export const usePreferredUnits = (): PreferredUnits => {
  const { data } = useGetUnitPreferenceQuery(undefined)
  return data?.preferredUnits ?? PreferredUnits.Kilometers
}

export interface PreferredUnitsResolution {
  /** The resolved preference, or the {@link PreferredUnits.Kilometers} default once settled. */
  units: PreferredUnits
  /**
   * `false` until the preference query has settled (success OR error). The write
   * path must wait for this before interpreting a typed distance, so a miles
   * runner's input is never converted against the loading-time km fallback.
   */
  isResolved: boolean
}

/**
 * Reads the unit preference for the **write** path — pairs the resolved unit with
 * a settled flag so a numeric write (the log form) can hold off until the runner's
 * unit is actually known, rather than converting against the display fallback.
 *
 * `isResolved` flips true on both success and error: once the query has settled,
 * the {@link PreferredUnits.Kilometers} default is authoritative (the `GET` is
 * read-or-default 200, so an error means the whole API — including the log `POST`
 * — is unavailable, and no wrong-unit write can be persisted anyway).
 */
export const usePreferredUnitsResolution = (): PreferredUnitsResolution => {
  const { data, isLoading } = useGetUnitPreferenceQuery(undefined)
  return {
    units: data?.preferredUnits ?? PreferredUnits.Kilometers,
    isResolved: !isLoading,
  }
}
