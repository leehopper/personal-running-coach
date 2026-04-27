import { apiSlice } from '~/api/api-slice'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

/**
 * Wire shape for the optional regeneration intent block on
 * `POST /api/v1/plan/regenerate`. Mirrors
 * `RunCoach.Api.Modules.Training.Plan.RegenerationIntentRequestDto` —
 * `freeText` is capped at 500 characters server-side BEFORE sanitization.
 */
export interface RegenerationIntentRequestDto {
  freeText: string
}

/**
 * Request body for `POST /api/v1/plan/regenerate`. Mirrors
 * `RunCoach.Api.Modules.Training.Plan.RegeneratePlanRequestDto`. The
 * client supplies `idempotencyKey` via `crypto.randomUUID()` so retries
 * short-circuit to the same `planId` rather than producing a second
 * Plan stream (spec 13 § Unit 5 R05.1).
 */
export interface RegeneratePlanRequestDto {
  idempotencyKey: string
  intent?: RegenerationIntentRequestDto
}

/**
 * Response shape returned by `POST /api/v1/plan/regenerate`. Mirrors
 * `RunCoach.Api.Modules.Training.Plan.RegeneratePlanResponse`. `status`
 * is always `"generated"` on the success path; modeled as a string so
 * future slices can introduce richer values without breaking existing
 * clients.
 */
export interface RegeneratePlanResponseDto {
  planId: string
  status: string
}

// Plan endpoints are injected into the root `apiSlice` so every request
// shares the cookie + antiforgery base query from Slice 0. Routes match
// `backend/src/RunCoach.Api/Modules/Training/Plan/PlanRenderingController.cs`
// with the global `/api/v1` prefix supplied by `base-query.ts#baseUrl`.
//
// `getCurrentPlan` is tagged `Plan` so the `regeneratePlan` mutation can
// declare `invalidatesTags: ['Plan']` and have the home surface refetch the
// fresh projection automatically (spec 13 § Unit 4 R04.1, § Unit 5).
export const planApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getCurrentPlan: builder.query<PlanProjectionDto, undefined>({
      query: () => ({ url: '/v1/plan/current', method: 'GET' }),
      providesTags: ['Plan'],
    }),
    regeneratePlan: builder.mutation<RegeneratePlanResponseDto, RegeneratePlanRequestDto>({
      query: (body) => ({
        url: '/v1/plan/regenerate',
        method: 'POST',
        body,
      }),
      invalidatesTags: ['Plan'],
    }),
  }),
})

export const { useGetCurrentPlanQuery, useLazyGetCurrentPlanQuery, useRegeneratePlanMutation } =
  planApi
