import { apiSlice } from '~/api/api-slice'
import type {
  AuthResponseDto,
  LoginRequestDto,
  RegisterRequestDto,
} from '~/modules/auth/models/auth.model'

// Auth endpoints are injected into the root `apiSlice` so every request
// shares the same middleware (credentials: 'include', XSRF header on
// mutations, 401 handler). Routes match `backend/src/RunCoach.Api/Modules/
// Identity/AuthController.cs` with the global `/api/v1` prefix supplied by
// `base-query.ts#baseUrl`.
export const authApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    xsrf: builder.query<undefined, undefined>({
      query: () => ({ url: '/v1/auth/xsrf', method: 'GET' }),
    }),
    me: builder.query<AuthResponseDto, undefined>({
      query: () => ({ url: '/v1/auth/me', method: 'GET' }),
      providesTags: ['Auth'],
    }),
    register: builder.mutation<AuthResponseDto, RegisterRequestDto>({
      query: (body) => ({ url: '/v1/auth/register', method: 'POST', body }),
      invalidatesTags: ['Auth'],
    }),
    login: builder.mutation<AuthResponseDto, LoginRequestDto>({
      query: (body) => ({ url: '/v1/auth/login', method: 'POST', body }),
      invalidatesTags: ['Auth'],
    }),
    logout: builder.mutation<undefined, undefined>({
      query: () => ({ url: '/v1/auth/logout', method: 'POST' }),
      invalidatesTags: ['Auth'],
    }),
  }),
})

export const {
  useXsrfQuery,
  useMeQuery,
  useLazyXsrfQuery,
  useLazyMeQuery,
  useRegisterMutation,
  useLoginMutation,
  useLogoutMutation,
} = authApi
