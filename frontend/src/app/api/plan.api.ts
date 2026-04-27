import { apiSlice } from '~/api/api-slice'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

// Plan endpoints are injected into the root `apiSlice` so every request
// shares the cookie + antiforgery base query from Slice 0. Routes match
// `backend/src/RunCoach.Api/Modules/Training/Plan/PlanRenderingController.cs`
// with the global `/api/v1` prefix supplied by `base-query.ts#baseUrl`.
//
// `getCurrentPlan` is tagged `Plan` so Unit 5's `regeneratePlan` mutation can
// declare `invalidatesTags: ['Plan']` and have the home surface refetch the
// fresh projection automatically (spec 13 § Unit 4 R04.1, § Unit 5).
export const planApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getCurrentPlan: builder.query<PlanProjectionDto, undefined>({
      query: () => ({ url: '/v1/plan/current', method: 'GET' }),
      providesTags: ['Plan'],
    }),
  }),
})

export const { useGetCurrentPlanQuery, useLazyGetCurrentPlanQuery } = planApi
