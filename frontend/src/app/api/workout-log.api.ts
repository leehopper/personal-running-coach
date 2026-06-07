import { apiSlice } from '~/api/api-slice'
import type { CreateWorkoutLogRequest } from '~/api/generated'

/**
 * Response shape returned by `POST /api/v1/workouts/logs`. Mirrors
 * `RunCoach.Api.Modules.Training.Workouts.CreateWorkoutLogResponseDto`. The
 * server replies `201 Created` with the persisted log's id; the generated Zod
 * response schema is `z.void()` (Swashbuckle emits no 201 body schema), so this
 * hand-mirrored type is the source of truth for the mutation's response generic.
 */
export interface CreateWorkoutLogResponseDto {
  workoutLogId: string
}

// Workout-log endpoints are injected into the root `apiSlice` so every request
// shares the same cookie + antiforgery base query (the X-XSRF-TOKEN header is
// added automatically for mutations by `base-query.ts`). URL segments are
// relative to the global `/api` prefix supplied by the base query — the
// generated RTK endpoint uses the full `/api/v1/...` path and is intentionally
// unused at runtime (it would double the prefix); this hand-written wrapper is
// the consumed surface (the auth/plan/onboarding convention).
//
// `createWorkoutLog` invalidates the `WorkoutLog` tag so any mounted
// workout-history query refetches after a successful log (the history surface
// lands in PR7). The client-generated `idempotencyKey` rides inside the body
// (DEC-077), exactly like `regeneratePlan`.
export const workoutLogApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    createWorkoutLog: builder.mutation<CreateWorkoutLogResponseDto, CreateWorkoutLogRequest>({
      query: (body) => ({
        url: '/v1/workouts/logs',
        method: 'POST',
        body,
      }),
      invalidatesTags: ['WorkoutLog'],
    }),
  }),
})

/** Auto-generated RTK Query hook for the workout-log create endpoint. */
export const { useCreateWorkoutLogMutation } = workoutLogApi
