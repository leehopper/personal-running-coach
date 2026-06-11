import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import type { ConversationTurnsResponseDto } from '~/modules/coaching/models/conversation.model'
import { conversationApi } from './conversation.api'

// Dispatch the endpoint thunk directly with `fetch` stubbed at the global level
// so the `query: () => ({...})` factory actually executes (the page spec mocks
// the hook and never reaches the factory). Mirrors plan.api.spec.ts.
const OriginalRequest = globalThis.Request
class PatchedRequest extends OriginalRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    if (typeof input === 'string' && input.startsWith('/')) {
      super(new URL(input, 'https://localhost:5173').toString(), init)
      return
    }
    super(input, init)
  }
}

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const makeStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(apiSlice.middleware),
  })

const EMPTY_RESPONSE: ConversationTurnsResponseDto = { turns: [] }

describe('conversationApi.getConversationTurns query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(jsonResponse(EMPTY_RESPONSE))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('issues a GET to /api/v1/conversation/turns and surfaces the turns payload', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      conversationApi.endpoints.getConversationTurns.initiate(undefined),
    )

    expect(result.data).toEqual(EMPTY_RESPONSE)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request).toBeInstanceOf(Request)
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/conversation/turns')
  })
})
