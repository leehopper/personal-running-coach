import { useGetCurrentPlanQuery } from '~/api/plan.api'
import type {
  MesoWeekTemplateDto,
  MicroWorkoutCardDto,
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

const DAY_MS = 86_400_000

/**
 * Parse an ISO `YYYY-MM-DD` plan start date into a UTC-midnight epoch, or
 * `undefined` when the string is absent or malformed. UTC midnight keeps the
 * day-count subtraction free of DST artefacts.
 */
const parsePlanStartUtc = (planStartDate: string): number | undefined => {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(planStartDate)
  if (match === null) return undefined
  const utc = Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  return Number.isNaN(utc) ? undefined : utc
}

/**
 * The pre-anchor heuristic, retained as a defensive fallback for streams that
 * carry no usable `planStartDate`: the lowest-numbered populated week in
 * `microWorkoutsByWeek`, then the first meso template's week, then week 1.
 */
const resolveLowestPopulatedWeek = (plan: PlanProjectionDto): number => {
  const populatedWeeks = Object.keys(plan.microWorkoutsByWeek)
    .map((key) => Number.parseInt(key, 10))
    .filter((value) => Number.isFinite(value) && value >= 1)
    .sort((left, right) => left - right)
  const firstPopulated = populatedWeeks[0]
  if (firstPopulated !== undefined) {
    return firstPopulated
  }
  const firstMeso = plan.mesoWeeks.find((week) => week.weekNumber >= 1)
  return firstMeso?.weekNumber ?? 1
}

/**
 * Returns the 1-based current training week, derived from the plan's calendar
 * anchor (`planStartDate`, slice-2b Unit 1) relative to `referenceDate`
 * (today by default): `floor((referenceDate − planStartDate).days / 7) + 1`.
 *
 * The result is clamped into the range of weeks that actually carry a meso
 * template, so the caller always lands on a renderable week — a date before the
 * plan starts resolves to its first week, a date past the generated weeks to its
 * last. When `planStartDate` is missing or malformed (or no templates exist),
 * it falls back to the legacy lowest-populated-week heuristic so the surface
 * still renders a defined week without throwing.
 *
 * `referenceDate` is injectable purely for deterministic tests; production
 * callers omit it and get the current date.
 */
export const resolveCurrentWeek = (
  plan: PlanProjectionDto,
  referenceDate: Date = new Date(),
): number => {
  const templateWeeks = plan.mesoWeeks
    .map((week) => week.weekNumber)
    .filter((value) => Number.isFinite(value) && value >= 1)
    .sort((left, right) => left - right)

  const planStartUtc = parsePlanStartUtc(plan.planStartDate)
  const firstWeek = templateWeeks[0]
  const lastWeek = templateWeeks[templateWeeks.length - 1]
  if (planStartUtc === undefined || firstWeek === undefined || lastWeek === undefined) {
    return resolveLowestPopulatedWeek(plan)
  }

  const referenceUtc = Date.UTC(
    referenceDate.getFullYear(),
    referenceDate.getMonth(),
    referenceDate.getDate(),
  )
  const elapsedDays = Math.floor((referenceUtc - planStartUtc) / DAY_MS)
  const dateDerivedWeek = Math.floor(elapsedDays / 7) + 1

  return Math.min(Math.max(dateDerivedWeek, firstWeek), lastWeek)
}

/**
 * Pull the meso template for the current 1-based week. Returns `undefined`
 * when the plan has no matching template — the caller renders a defensive
 * empty state in that case.
 */
export const findCurrentMesoWeek = (
  plan: PlanProjectionDto,
  currentWeek: number,
): MesoWeekTemplateDto | undefined => plan.mesoWeeks.find((week) => week.weekNumber === currentWeek)

/**
 * Pull the detailed workouts for the current 1-based week from the
 * projection's `microWorkoutsByWeek` map. Returns an empty array when the
 * week has no pre-generated micro list yet.
 */
export const findCurrentWeekWorkouts = (
  plan: PlanProjectionDto,
  currentWeek: number,
): readonly MicroWorkoutCardDto[] => plan.microWorkoutsByWeek[currentWeek]?.workouts ?? []

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
