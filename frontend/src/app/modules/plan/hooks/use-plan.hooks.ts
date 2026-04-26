import { useGetCurrentPlanQuery } from '~/api/plan.api'
import type {
  MesoWeekTemplate,
  MicroWorkoutCard as MicroWorkoutDto,
  PlanProjectionDto,
} from '~/modules/plan/models/plan.model'

/**
 * Loading + error + data shape exposed by `usePlan`. Mirrors the surface
 * area of RTK Query's auto-generated query hook so the consuming page can
 * branch on `isLoading` / `isError` without reaching back into the cache
 * shape directly.
 */
export interface UsePlanReturn {
  plan: PlanProjectionDto | undefined
  isLoading: boolean
  isError: boolean
  /** True when the wire response was 404 — i.e. no plan stream yet. */
  isNotFound: boolean
  error: unknown
  refetch: () => void
}

/**
 * Top-level hook the home page uses to fetch the current plan. Wraps
 * `useGetCurrentPlanQuery` and surfaces a stable 404 flag so the page can
 * render the "no plan yet" defensive state without parsing the opaque
 * RTK Query error union inline.
 *
 * Spec § Unit 4 R04.1, R04.3.
 */
export const usePlan = (): UsePlanReturn => {
  const query = useGetCurrentPlanQuery(undefined)
  return {
    plan: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    isNotFound: isErrorStatus(query.error, 404),
    error: query.error,
    refetch: () => {
      query.refetch()
    },
  }
}

/**
 * 1-based current week derived from the projection. Slice 1 emits exactly
 * one populated week (week 1) — that is "now" by construction. Returning
 * the lowest-numbered populated week keeps the helper forward-compatible
 * with later slices that pre-generate additional weeks without changing
 * the home page's call site.
 */
export const resolveCurrentWeek = (plan: PlanProjectionDto): number => {
  const populatedWeeks = Object.keys(plan.microWorkoutsByWeek)
    .map((key) => Number.parseInt(key, 10))
    .filter((value) => Number.isFinite(value) && value >= 1)
    .sort((left, right) => left - right)
  if (populatedWeeks.length > 0) {
    return populatedWeeks[0] as number
  }
  // Fall back to the first meso template's week number when no micro
  // workouts have been pre-generated yet (defensive — Slice 1 always
  // populates week 1).
  const firstMeso = plan.mesoWeeks.find((week) => week.weekNumber >= 1)
  return firstMeso?.weekNumber ?? 1
}

/**
 * Pull the meso template for the current 1-based week. Returns `undefined`
 * when the plan has no matching template — the caller renders a defensive
 * empty state in that case.
 */
export const findCurrentMesoWeek = (
  plan: PlanProjectionDto,
  currentWeek: number,
): MesoWeekTemplate | undefined => plan.mesoWeeks.find((week) => week.weekNumber === currentWeek)

/**
 * Pull the detailed workouts for the current 1-based week from the
 * projection's `microWorkoutsByWeek` map. Returns an empty array when the
 * week has no pre-generated micro list yet.
 */
export const findCurrentWeekWorkouts = (
  plan: PlanProjectionDto,
  currentWeek: number,
): readonly MicroWorkoutDto[] => plan.microWorkoutsByWeek[currentWeek]?.workouts ?? []

/**
 * RTK Query surfaces an opaque `FetchBaseQueryError | SerializedError`
 * union. The home page (and the route guard above it) treat 404 as "no
 * plan stream yet" — every other error stays generic.
 */
const isErrorStatus = (error: unknown, expected: number): boolean => {
  if (typeof error !== 'object' || error === null) return false
  const candidate = error as { status?: unknown }
  return candidate.status === expected
}
