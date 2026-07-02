import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits } from '~/api/generated'

interface UnitPreference {
  preferredUnits: PreferredUnits
}

// The hook only reads `data` off the query result, so a plain hoisted ref
// standing in for `useGetUnitPreferenceQuery` keeps the test free of a Redux
// store while exercising the real fallback/selection logic.
const { getQueryRef } = vi.hoisted(() => ({
  getQueryRef: { data: undefined as UnitPreference | undefined },
}))

vi.mock('~/api/settings.api', () => ({
  useGetUnitPreferenceQuery: () => getQueryRef,
}))

import { usePreferredUnits } from './use-preferred-units.hooks'

describe('usePreferredUnits', () => {
  beforeEach(() => {
    getQueryRef.data = undefined
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

  it('returns the persisted Kilometers preference (0 is not treated as absent)', () => {
    getQueryRef.data = { preferredUnits: PreferredUnits.Kilometers }
    const { result } = renderHook(() => usePreferredUnits())
    expect(result.current).toBe(PreferredUnits.Kilometers)
  })
})
