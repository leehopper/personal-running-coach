import { useGetConversationTurnsQuery } from '~/api/conversation.api'
import type { ConversationTurnDto } from '~/modules/coaching/models/conversation.model'

/**
 * Loading + error + data shape exposed by `useConversationTurns`. Mirrors
 * the surface area of RTK Query's auto-generated query hook so the consuming
 * page can branch without reaching back into the cache shape directly
 * (the `usePlan` convention).
 */
export interface UseConversationTurnsReturn {
  /** The runner's turns, newest-first; empty until loaded. */
  turns: readonly ConversationTurnDto[]
  isLoading: boolean
  isError: boolean
}

// Stable fallback so consumers get the same array identity across renders
// while the query is in flight (avoids re-render churn from a fresh `[]`).
const NO_TURNS: readonly ConversationTurnDto[] = []

/**
 * Hook the home surface uses to fetch the read-only "Explain-the-change"
 * turns. The panel is supplementary to the plan view, so consumers render
 * nothing (rather than an error surface) on failure — the flags exist for
 * that branch, not for a loading spinner.
 */
export const useConversationTurns = (): UseConversationTurnsReturn => {
  const query = useGetConversationTurnsQuery(undefined)
  return {
    turns: query.data?.turns ?? NO_TURNS,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
