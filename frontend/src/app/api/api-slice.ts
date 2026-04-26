import { createApi } from '@reduxjs/toolkit/query/react'
import { baseQueryWith401Handler } from '~/api/base-query'

// Root RTK Query API slice. Feature APIs (`auth.api.ts`, future
// `plan.api.ts`, etc.) call `apiSlice.injectEndpoints({ ... })` so the
// cache namespace and middleware are shared across the whole app.
export const apiSlice = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWith401Handler,
  tagTypes: ['Auth', 'Onboarding'],
  endpoints: () => ({}),
})
