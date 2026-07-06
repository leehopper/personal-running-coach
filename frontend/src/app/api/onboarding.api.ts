import { apiSlice } from '~/api/api-slice'
import type {
  OnboardingStateDto,
  SubmitStructuredAnswersRequest,
} from '~/modules/onboarding/models/onboarding.model'

// Onboarding endpoints are injected into the root `apiSlice` so every request
// shares the cookie + antiforgery base query from Slice 0 — `credentials:
// 'include'` lifts the auth cookie, `prepareHeaders` adds the `X-XSRF-TOKEN`
// header on mutations from the double-submit cookie pair. Routes match
// `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/OnboardingController.cs`
// with the global `/api/v1` prefix supplied by `base-query.ts#baseUrl`.
//
// Slice 4C-onboarding replaced the turn-based chat with the deterministic,
// form-first intake (DEC-086): `submitStructuredAnswers` posts every completed
// topic in one request to `POST /answers`, and the per-turn `submitOnboardingTurn`
// / `reviseAnswer` endpoints were removed. `getOnboardingState` is retained for
// the redirect guard and the resume-hydrate.
export const onboardingApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getOnboardingState: builder.query<OnboardingStateDto, undefined>({
      query: () => ({ url: '/v1/onboarding/state', method: 'GET' }),
      providesTags: ['Onboarding'],
    }),
    submitStructuredAnswers: builder.mutation<OnboardingStateDto, SubmitStructuredAnswersRequest>({
      query: (body) => ({
        url: '/v1/onboarding/answers',
        method: 'POST',
        body,
      }),
      // Success-gated invalidation: on a completed submission the reloaded state
      // reports `isComplete: true`, so refetching `getOnboardingState` lets the
      // `OnboardingRedirectGuard` (subscribed to the same tag) route the runner to
      // `/` in the same interaction. The callback form returns `[]` on error — RTK
      // Query applies a *static* `invalidatesTags` array on rejected-with-value
      // mutations too, and a rejected submit (400/409/422) leaves onboarding
      // exactly where it was, so a refetch would be pointless. Mirrors
      // `settings.api.ts#putUnitPreference`.
      invalidatesTags: (_result, error) => (error === undefined ? ['Onboarding'] : []),
    }),
  }),
})

export const {
  useGetOnboardingStateQuery,
  useLazyGetOnboardingStateQuery,
  useSubmitStructuredAnswersMutation,
} = onboardingApi
