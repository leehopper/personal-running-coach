import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
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

  it('two concurrent subscribers both update when recordLastTraceId fires', () => {
    const { result: result1 } = renderHook(() => useLastTraceId())
    const { result: result2 } = renderHook(() => useLastTraceId())

    expect(result1.current).toBeNull()
    expect(result2.current).toBeNull()

    act(() => {
      recordLastTraceId('a'.repeat(32))
    })

    expect(result1.current).toBe('a'.repeat(32))
    expect(result2.current).toBe('a'.repeat(32))
  })

  it('unsubscribing one subscriber via cleanup leaves remaining subscribers intact', () => {
    // `useSyncExternalStore` registers its listener when the hook mounts and
    // calls the unsubscribe return value (i.e. `listeners.delete(listener)`)
    // when the component unmounts. Spy on `Set.prototype.delete` to assert
    // the unsubscribe closure actually calls `Set.delete` — otherwise the
    // listener leaks and the `Set` grows without bound.
    const deleteSpy = vi.spyOn(Set.prototype, 'delete')

    const { unmount } = renderHook(() => useLastTraceId())
    const { result: result2 } = renderHook(() => useLastTraceId())

    act(() => {
      unmount()
    })

    // The unsubscribe closure returned by `subscribe` must have called
    // `Set.delete` exactly once (for the unmounted hook's listener).
    expect(deleteSpy).toHaveBeenCalledTimes(1)

    deleteSpy.mockRestore()

    // The remaining subscriber must still receive updates after the first
    // hook's listener has been removed from the `Set`.
    act(() => {
      recordLastTraceId('b'.repeat(32))
    })

    expect(result2.current).toBe('b'.repeat(32))
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
