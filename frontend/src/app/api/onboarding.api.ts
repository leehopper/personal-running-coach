import { apiSlice } from '~/api/api-slice'
import type {
  OnboardingStateDto,
  OnboardingTurnRequestDto,
  OnboardingTurnResponse,
  ReviseAnswerRequestDto,
} from '~/modules/onboarding/models/onboarding.model'

// Onboarding endpoints are injected into the root `apiSlice` so every request
// shares the cookie + antiforgery base query from Slice 0 — `credentials:
// 'include'` lifts the auth cookie, `prepareHeaders` adds the
// `X-XSRF-TOKEN` header on mutations from the double-submit cookie pair.
// Routes match `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/
// OnboardingController.cs` with the global `/api/v1` prefix supplied by
// `base-query.ts#baseUrl`.
//
// The response shape for `submitOnboardingTurn` is the discriminated union
// declared in `~/modules/onboarding/models/onboarding.model.ts`. Runtime
// validation lives in the corresponding Zod schema and is applied by the
// page-level hook before the union is narrowed — keeping the RTK cache
// untouched lets us preserve raw responses for replay debugging.
export const onboardingApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getOnboardingState: builder.query<OnboardingStateDto, undefined>({
      query: () => ({ url: '/v1/onboarding/state', method: 'GET' }),
      providesTags: ['Onboarding'],
    }),
    submitOnboardingTurn: builder.mutation<OnboardingTurnResponse, OnboardingTurnRequestDto>({
      query: (body) => ({
        url: '/v1/onboarding/turns',
        method: 'POST',
        body,
      }),
      invalidatesTags: ['Onboarding'],
    }),
    reviseAnswer: builder.mutation<OnboardingStateDto, ReviseAnswerRequestDto>({
      query: (body) => ({
        url: '/v1/onboarding/answers/revise',
        method: 'POST',
        body,
      }),
      invalidatesTags: ['Onboarding'],
    }),
  }),
})

export const {
  useGetOnboardingStateQuery,
  useLazyGetOnboardingStateQuery,
  useSubmitOnboardingTurnMutation,
  useReviseAnswerMutation,
} = onboardingApi
