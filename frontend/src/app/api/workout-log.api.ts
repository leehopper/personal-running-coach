import { apiSlice } from '~/api/api-slice'
import type {
  CreateWorkoutLogRequest,
  CreateWorkoutLogResponseDto,
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
// in the same interaction. Invalidation fires only on a fulfilled mutation —
// a failed create refetches nothing.
export const workoutLogApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    createWorkoutLog: builder.mutation<CreateWorkoutLogResponseDto, CreateWorkoutLogRequest>({
      query: (body) => ({
        url: '/v1/workouts/logs',
        method: 'POST',
        body,
      }),
      invalidatesTags: ['WorkoutLog', 'Plan', 'Conversation'],
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
  }),
})

/** Auto-generated RTK Query hooks for the workout-log endpoints. */
export const { useCreateWorkoutLogMutation, useGetWorkoutLogHistoryInfiniteQuery } = workoutLogApi
