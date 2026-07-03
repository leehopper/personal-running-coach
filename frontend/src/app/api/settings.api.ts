import { apiSlice } from '~/api/api-slice'
import type { UnitPreferenceDto } from '~/api/generated'

// Settings endpoints are injected into the root `apiSlice` so every request
// shares the same cookie + antiforgery base query. URL segments (e.g.
// `/v1/settings/units`) are relative to the global `/api` prefix supplied by
// the base query.
//
// `getUnitPreference` is tagged `UserSettings` so the `putUnitPreference`
// mutation can invalidate it and have the preference refetch after a save. The
// callback form gates invalidation on success — RTK Query applies a *static*
// `invalidatesTags` array on rejected-with-value mutations too (an HTTP failure
// surfaces as a rejected-with-value base-query error), so returning `[]` on
// error keeps a failed save from firing a pointless refetch. We deliberately
// avoid `updateQueryData` optimism — it silently no-ops on a cold cache
// (uninitialized or pending-with-no-data), which is exactly the state a
// first-visit Settings page is in.
export const settingsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getUnitPreference: builder.query<UnitPreferenceDto, undefined>({
      query: () => ({ url: '/v1/settings/units', method: 'GET' }),
      providesTags: ['UserSettings'],
    }),
    putUnitPreference: builder.mutation<UnitPreferenceDto, UnitPreferenceDto>({
      query: (body) => ({
        url: '/v1/settings/units',
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_result, error) => (error === undefined ? ['UserSettings'] : []),
    }),
  }),
})

/** Auto-generated RTK Query hooks for the settings endpoints. */
export const { useGetUnitPreferenceQuery, usePutUnitPreferenceMutation } = settingsApi
