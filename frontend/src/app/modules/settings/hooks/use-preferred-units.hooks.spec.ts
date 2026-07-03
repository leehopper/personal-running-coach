import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits } from '~/api/generated'

interface UnitPreference {
  preferredUnits: PreferredUnits
}

// The hooks read `data` + the settled flags off the query result, so a plain
// hoisted ref standing in for `useGetUnitPreferenceQuery` keeps the test free of
// a Redux store while exercising the real fallback/selection logic.
const { getQueryRef, refetchMock } = vi.hoisted(() => {
  const refetchMock = vi.fn()
  return {
    refetchMock,
    getQueryRef: {
      data: undefined as UnitPreference | undefined,
      isSuccess: false,
      isError: false,
      refetch: refetchMock,
    },
  }
})

vi.mock('~/api/settings.api', () => ({
  useGetUnitPreferenceQuery: () => getQueryRef,
}))

import { usePreferredUnits, usePreferredUnitsResolution } from './use-preferred-units.hooks'

describe('usePreferredUnits', () => {
  beforeEach(() => {
    getQueryRef.data = undefined
    getQueryRef.isSuccess = false
    getQueryRef.isError = false
    refetchMock.mockClear()
  })

  it('falls back to Kilometers while the preference is unresolved', () => {
    getQueryRef.data = undefined
    const { result } = renderHook(() => usePreferredUnits())
    expect(result.current).toBe(PreferredUnits.Kilometers)
  })

  it('returns the persisted Miles preference', () => {
    getQueryRef.data = { preferredUnits: PreferredUnits.Miles }
    const { result } = renderHook(() => usePreferredUnits())
    expect(result.current).toBe(PreferredUnits.Miles)
  })

  it('returns the persisted Kilometers preference', () => {
    // Kilometers is the enum value 0, which also happens to be the fallback, so
    // this case cannot by itself distinguish `??` from `||` — over the whole
    // {undefined, 0, 1} domain the two operators are provably equivalent here.
    // It still guards that a persisted Kilometers reads back as Kilometers.
    getQueryRef.data = { preferredUnits: PreferredUnits.Kilometers }
    const { result } = renderHook(() => usePreferredUnits())
    expect(result.current).toBe(PreferredUnits.Kilometers)
  })
})

describe('usePreferredUnitsResolution', () => {
  beforeEach(() => {
    getQueryRef.data = undefined
    getQueryRef.isSuccess = false
    getQueryRef.isError = false
    refetchMock.mockClear()
  })

  it('is unresolved (Kilometers) while the preference query is loading', () => {
    getQueryRef.data = undefined
    getQueryRef.isSuccess = false
    getQueryRef.isError = false
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current.units).toBe(PreferredUnits.Kilometers)
    expect(result.current.isResolved).toBe(false)
    expect(result.current.isError).toBe(false)
  })

  it('resolves to the persisted Miles preference once the query succeeds', () => {
    getQueryRef.data = { preferredUnits: PreferredUnits.Miles }
    getQueryRef.isSuccess = true
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current.units).toBe(PreferredUnits.Miles)
    expect(result.current.isResolved).toBe(true)
    expect(result.current.isError).toBe(false)
  })

  it('resolves to the Kilometers default once the query succeeds with no row (read-or-default 200)', () => {
    getQueryRef.data = undefined
    getQueryRef.isSuccess = true
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current.units).toBe(PreferredUnits.Kilometers)
    expect(result.current.isResolved).toBe(true)
    expect(result.current.isError).toBe(false)
  })

  it('reports isError (never resolved) when the preference query errors, so the write path stays gated', () => {
    // RTK Query's `isLoading` goes false on error too; gating on `isSuccess`
    // keeps an errored settings GET from being treated as a resolved km default,
    // which would silently convert a Miles runner's distance at km magnitude.
    getQueryRef.data = undefined
    getQueryRef.isSuccess = false
    getQueryRef.isError = true
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current.isResolved).toBe(false)
    expect(result.current.isError).toBe(true)
    expect(result.current.units).toBe(PreferredUnits.Kilometers)
  })

  it('exposes refetch for the error-state retry affordance', () => {
    getQueryRef.isError = true
    const { result } = renderHook(() => usePreferredUnitsResolution())
    result.current.refetch()
    expect(refetchMock).toHaveBeenCalledTimes(1)
  })
})
