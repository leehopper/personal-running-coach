import { apiSlice } from '~/api/api-slice'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

// Plan endpoints are injected into the root `apiSlice` so every request
// shares the same cookie + antiforgery base query. URL segments (e.g.
// `/v1/plan/current`) are relative to the global `/api` prefix supplied by
// the base query.
//
// `getCurrentPlan` carries the `Plan` tag so any mutation that invalidates
// `Plan` causes the home surface to refetch the projection automatically
// (spec 13 § Unit 4 R04.1).
export const planApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getCurrentPlan: builder.query<PlanProjectionDto, undefined>({
      query: () => ({ url: '/v1/plan/current', method: 'GET' }),
      providesTags: ['Plan'],
    }),
  }),
})

/** Auto-generated RTK Query hooks for the plan endpoints. */
export const { useGetCurrentPlanQuery, useLazyGetCurrentPlanQuery } = planApi
