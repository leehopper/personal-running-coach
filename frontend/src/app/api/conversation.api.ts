import { apiSlice } from '~/api/api-slice'
import type { ConversationTurnsResponseDto } from '~/modules/coaching/models/conversation.model'

// Conversation endpoints are injected into the root `apiSlice` so every
// request shares the same cookie + antiforgery base query. The read takes no
// antiforgery token (`base-query.ts` only adds X-XSRF-TOKEN to mutations).
// URL segments are relative to the global `/api` prefix supplied by the base
// query.
//
// `getConversationTurns` is tagged `Conversation` so the `createWorkoutLog`
// mutation can declare `invalidatesTags: [..., 'Conversation']` and the
// read-only "Explain-the-change" panel refetches in the same interaction as
// the plan view (spec 17 § Unit 7).
export const conversationApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getConversationTurns: builder.query<ConversationTurnsResponseDto, undefined>({
      query: () => ({ url: '/v1/conversation/turns', method: 'GET' }),
      providesTags: ['Conversation'],
    }),
  }),
})

/** Auto-generated RTK Query hooks for the conversation endpoints. */
export const { useGetConversationTurnsQuery, useLazyGetConversationTurnsQuery } = conversationApi
