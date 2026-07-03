import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits } from '~/api/generated'

interface UnitPreference {
  preferredUnits: PreferredUnits
}

// The hooks read `data` + `isLoading` off the query result, so a plain hoisted
// ref standing in for `useGetUnitPreferenceQuery` keeps the test free of a Redux
// store while exercising the real fallback/selection logic.
const { getQueryRef } = vi.hoisted(() => ({
  getQueryRef: { data: undefined as UnitPreference | undefined, isLoading: false },
}))

vi.mock('~/api/settings.api', () => ({
  useGetUnitPreferenceQuery: () => getQueryRef,
}))

import { usePreferredUnits, usePreferredUnitsResolution } from './use-preferred-units.hooks'

describe('usePreferredUnits', () => {
  beforeEach(() => {
    getQueryRef.data = undefined
    getQueryRef.isLoading = false
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
    getQueryRef.isLoading = false
  })

  it('is unresolved (Kilometers) while the preference query is loading', () => {
    getQueryRef.data = undefined
    getQueryRef.isLoading = true
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current).toEqual({ units: PreferredUnits.Kilometers, isResolved: false })
  })

  it('resolves to the persisted Miles preference once the query settles', () => {
    getQueryRef.data = { preferredUnits: PreferredUnits.Miles }
    getQueryRef.isLoading = false
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current).toEqual({ units: PreferredUnits.Miles, isResolved: true })
  })

  it('resolves to the Kilometers default once settled with no row (read-or-default 200)', () => {
    getQueryRef.data = undefined
    getQueryRef.isLoading = false
    const { result } = renderHook(() => usePreferredUnitsResolution())
    expect(result.current).toEqual({ units: PreferredUnits.Kilometers, isResolved: true })
  })
})
