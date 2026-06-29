import { apiSlice } from '~/api/api-slice'
import type {
  ConfirmConversationalLogRequestDto,
  ConfirmConversationalLogResponseDto,
} from '~/api/generated'
import type {
  ConversationTimelineDto,
  ConversationTurnsResponseDto,
} from '~/modules/coaching/models/conversation.model'

// Conversation endpoints are injected into the root `apiSlice` so every
// request shares the same cookie + antiforgery base query. The reads take no
// antiforgery token (`base-query.ts` only adds X-XSRF-TOKEN to mutations).
// URL segments are relative to the global `/api` prefix supplied by the base
// query.
//
// `getConversationTurns` (newest-first proactive turns) feeds the read-only
// Slice 3 "Explain-the-change" panel; `getConversationTimeline` (oldest-first
// interactive + proactive union) feeds the Slice 4B interactive chat. Both are
// tagged `Conversation`, so the `createWorkoutLog` and `confirmConversationalLog`
// mutations refetch them in the same interaction as the plan view.
//
// The streaming Q&A endpoint (`POST /v1/conversation/messages`) is intentionally
// NOT modelled here — `fetchBaseQuery` cannot stream, so `useCoachStream`
// hand-rolls a `fetch` reader (see `coaching/hooks/use-coach-stream.hooks.ts`).
export const conversationApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getConversationTurns: builder.query<ConversationTurnsResponseDto, undefined>({
      query: () => ({ url: '/v1/conversation/turns', method: 'GET' }),
      providesTags: ['Conversation'],
    }),
    getConversationTimeline: builder.query<ConversationTimelineDto, undefined>({
      query: () => ({ url: '/v1/conversation/timeline', method: 'GET' }),
      providesTags: ['Conversation'],
    }),
    confirmConversationalLog: builder.mutation<
      ConfirmConversationalLogResponseDto,
      ConfirmConversationalLogRequestDto
    >({
      query: (body) => ({ url: '/v1/conversation/logs/confirm', method: 'POST', body }),
      // A confirm commits one workout log, can adapt the plan, and appends an
      // ack turn — the same triad as `createWorkoutLog`. The callback form
      // returns the tags only on success: RTK applies a static array even on a
      // rejected-with-value (HTTP 4xx) mutation, so `[]` on error is the only
      // guard against a failed confirm replaying stale data.
      invalidatesTags: (_result, error) =>
        error === undefined ? ['WorkoutLog', 'Plan', 'Conversation'] : [],
    }),
  }),
})

/** Auto-generated RTK Query hooks for the conversation endpoints. */
export const {
  useGetConversationTurnsQuery,
  useLazyGetConversationTurnsQuery,
  useGetConversationTimelineQuery,
  useConfirmConversationalLogMutation,
} = conversationApi
