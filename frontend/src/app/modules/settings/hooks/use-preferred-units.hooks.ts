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

export interface UsePreferredUnitsResolutionReturn {
  /** The resolved preference, or the {@link PreferredUnits.Kilometers} default once settled successfully. */
  units: PreferredUnits
  /**
   * `false` until the preference query has settled **successfully**. The write
   * path must wait for this before interpreting a typed distance, so a miles
   * runner's input is never converted against the loading-time km fallback.
   */
  isResolved: boolean
  /**
   * `true` when the preference query settled in error. The write path must not
   * silently interpret a typed distance against the km fallback in this state:
   * `GET /settings/units` and the log `POST` are independent endpoints, so an
   * isolated GET failure can leave a miles runner submitting at km magnitude.
   * The `/log` page surfaces a retry instead of rendering the form.
   */
  isError: boolean
  /** Re-runs the preference query — wired to the error-state retry affordance. */
  refetch: () => void
}

/**
 * Reads the unit preference for the **write** path — pairs the resolved unit with
 * a settled flag so a numeric write (the log form) can hold off until the runner's
 * unit is actually known, rather than converting against the display fallback.
 *
 * `isResolved` is `true` only on **success**. An errored read is reported via
 * `isError` (not folded into the km fallback): a settings-GET failure does not
 * imply the log `POST` endpoint is also down, so silently defaulting to km could
 * persist a miles runner's distance at ~1.6× the wrong magnitude. Callers gate
 * the write on `isResolved` and offer a retry on `isError`.
 */
export const usePreferredUnitsResolution = (): UsePreferredUnitsResolutionReturn => {
  const { data, isSuccess, isError, refetch } = useGetUnitPreferenceQuery(undefined)
  return {
    units: data?.preferredUnits ?? PreferredUnits.Kilometers,
    isResolved: isSuccess,
    isError,
    refetch,
  }
}
