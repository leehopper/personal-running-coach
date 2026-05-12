import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  __resetLastTraceIdForTests,
  getLastTraceId,
  recordLastTraceId,
  useLastTraceId,
} from './last-trace-id'

describe('last-trace-id store', () => {
  afterEach(() => {
    __resetLastTraceIdForTests()
  })

  it('starts at null when no fetch has fired', () => {
    expect(getLastTraceId()).toBeNull()
  })

  it('records the latest trace-id and surfaces it via getLastTraceId', () => {
    recordLastTraceId('5b8efff798038103d269b633813fc60c')
    expect(getLastTraceId()).toBe('5b8efff798038103d269b633813fc60c')
  })

  it('useLastTraceId initial render returns the current value', () => {
    recordLastTraceId('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
    const { result } = renderHook(() => useLastTraceId())
    expect(result.current).toBe('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
  })

  it('useLastTraceId re-renders the subscribing component when the trace-id changes', () => {
    const { result } = renderHook(() => useLastTraceId())
    expect(result.current).toBeNull()

    act(() => {
      recordLastTraceId('11111111111111111111111111111111')
    })
    expect(result.current).toBe('11111111111111111111111111111111')

    act(() => {
      recordLastTraceId('22222222222222222222222222222222')
    })
    expect(result.current).toBe('22222222222222222222222222222222')
  })

  it('does not notify when the same trace-id is recorded twice in a row', () => {
    let renderCount = 0
    const { result } = renderHook(() => {
      renderCount += 1
      return useLastTraceId()
    })
    const initialRenders = renderCount

    act(() => {
      recordLastTraceId('33333333333333333333333333333333')
    })
    const afterFirst = renderCount

    act(() => {
      // Same id — burst-of-fan-out idempotency contract.
      recordLastTraceId('33333333333333333333333333333333')
    })
    expect(renderCount).toBe(afterFirst)
    expect(result.current).toBe('33333333333333333333333333333333')
    expect(initialRenders).toBeLessThan(afterFirst)
  })
})
