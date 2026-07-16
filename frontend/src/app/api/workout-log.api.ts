import { apiSlice } from '~/api/api-slice'
import type {
  CreateWorkoutLogRequest,
  CreateWorkoutLogResponseDto,
  PrescribedWorkoutDto,
  QueryWorkoutLogsRequestDto,
  QueryWorkoutLogsResponseDto,
} from '~/api/generated'

/**
 * Logs requested per history page. The backend clamps the page size
 * server-side (DEC-076 § C); this is the client's preferred batch for the
 * "Load older" infinite list.
 */
export const WORKOUT_HISTORY_PAGE_SIZE = 20

// Workout-log endpoints are injected into the root `apiSlice` so every request
// shares the same cookie + antiforgery base query (the X-XSRF-TOKEN header is
// added automatically for mutations by `base-query.ts`). URL segments are
// relative to the global `/api` prefix supplied by the base query — the
// generated RTK endpoint uses the full `/api/v1/...` path and is intentionally
// unused at runtime (it would double the prefix); this hand-written wrapper is
// the consumed surface (the auth/plan/onboarding convention).
//
// `createWorkoutLog` invalidates the `WorkoutLog` tag and `getWorkoutLogHistory`
// provides it, so logging a workout refetches the mounted history list. The
// client-generated `idempotencyKey` rides inside the create body (DEC-077),
// exactly like `regeneratePlan`.
//
// It also invalidates `Plan` + `Conversation` (spec 17 § Unit 7): a create
// can synchronously adapt the plan and append an explanation turn, so the
// home surface's plan view and the "Explain-the-change" panel both refetch
// in the same interaction. The callback form gates invalidation on success —
// RTK Query applies a *static* `invalidatesTags` array on rejected-with-value
// mutations too (an HTTP failure surfaces as a rejected-with-value base-query
// error), so returning `[]` on error keeps a failed submit from refetching
// the plan view or replaying a stale conversation panel.
export const workoutLogApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    createWorkoutLog: builder.mutation<CreateWorkoutLogResponseDto, CreateWorkoutLogRequest>({
      query: (body) => ({
        url: '/v1/workouts/logs',
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, error) =>
        error === undefined ? ['WorkoutLog', 'Plan', 'Conversation'] : [],
    }),
    // History over the DB-driven keyset query endpoint (POST is a read here, so
    // no antiforgery token — `base-query.ts` only adds X-XSRF-TOKEN to
    // mutations). Each page is a `{ logs, nextCursor }` envelope; the page param
    // is the opaque keyset cursor (`null` for the first/newest page). RTK stores
    // the `{ pages, pageParams }` structure and the component flattens
    // `pages.flatMap(p => p.logs)` for week grouping. `getNextPageParam`
    // returning `undefined` (nextCursor exhausted) drives `hasNextPage = false`,
    // hiding the "Load older" control.
    getWorkoutLogHistory: builder.infiniteQuery<
      QueryWorkoutLogsResponseDto,
      undefined,
      string | null
    >({
      infiniteQueryOptions: {
        initialPageParam: null,
        getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
      },
      query: ({ pageParam }) => ({
        url: '/v1/workouts/logs/query',
        method: 'POST',
        body: {
          limit: WORKOUT_HISTORY_PAGE_SIZE,
          cursor: pageParam,
        } satisfies QueryWorkoutLogsRequestDto,
      }),
      providesTags: ['WorkoutLog'],
    }),
    // The log form's prescribed-workout banner (Slice 4 D1): resolves the
    // active plan's prescription for a given date via the same
    // server-authoritative path the create flow uses. A read, so no
    // antiforgery token. The endpoint returns 200 with a literal `null` body
    // when the date resolves to no prescription (off-plan, rest day, no
    // active plan, or a malformed stored prescription) — the response type
    // allows `null` to carry that contract through to callers, rather than
    // treating absence as an error state. Tagged `Plan`, not a new tag: the
    // prescribed slot is plan-derived data, and both `createWorkoutLog` and
    // `regeneratePlan` already invalidate `Plan`, so the banner stays fresh
    // after either.
    getPrescribedWorkout: builder.query<PrescribedWorkoutDto | null, string>({
      query: (date) => ({
        url: '/v1/workouts/logs/prescribed',
        method: 'GET',
        params: { date },
      }),
      providesTags: ['Plan'],
    }),
  }),
})

/** Auto-generated RTK Query hooks for the workout-log endpoints. */
export const {
  useCreateWorkoutLogMutation,
  useGetWorkoutLogHistoryInfiniteQuery,
  useGetPrescribedWorkoutQuery,
} = workoutLogApi
