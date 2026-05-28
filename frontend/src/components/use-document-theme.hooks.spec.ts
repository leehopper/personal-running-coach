import { act, renderHook } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useDocumentTheme } from './use-document-theme.hooks'

describe('useDocumentTheme', () => {
  beforeEach(() => {
    document.documentElement.className = ''
  })

  afterEach(() => {
    document.documentElement.className = ''
    vi.restoreAllMocks()
  })

  it("reads 'light' from documentElement when the .dark class is absent on mount", () => {
    const { result } = renderHook(() => useDocumentTheme())
    expect(result.current).toBe('light')
  })

  it("reads 'dark' from documentElement when the .dark class is present on mount", () => {
    document.documentElement.classList.add('dark')
    const { result } = renderHook(() => useDocumentTheme())
    expect(result.current).toBe('dark')
  })

  it('updates when the .dark class toggles on documentElement', async () => {
    const { result } = renderHook(() => useDocumentTheme())
    expect(result.current).toBe('light')

    // MutationObserver callbacks are delivered as microtasks in jsdom; the
    // async `act` block flushes them so React commits the state update.
    await act(async () => {
      document.documentElement.classList.add('dark')
    })
    expect(result.current).toBe('dark')

    await act(async () => {
      document.documentElement.classList.remove('dark')
    })
    expect(result.current).toBe('light')
  })

  it('disconnects the MutationObserver on unmount', async () => {
    const disconnectSpy = vi.spyOn(MutationObserver.prototype, 'disconnect')
    const { result, unmount } = renderHook(() => useDocumentTheme())
    expect(result.current).toBe('light')

    unmount()
    expect(disconnectSpy).toHaveBeenCalled()

    // After unmount the observer must no longer be subscribed — flipping the
    // class now is a no-op as far as the (already-unmounted) hook is
    // concerned. `result.current` is frozen at the last rendered value.
    await act(async () => {
      document.documentElement.classList.add('dark')
    })
    expect(result.current).toBe('light')
  })

  it('eagerly re-syncs after mount when the class changed between render and effect', () => {
    // Under StrictMode React mounts the component twice. The eager `sync()`
    // call inside the effect ensures the second mount picks up the current
    // class even if it changed between the render-phase initializer and the
    // effect firing — without it the hook would only react to *subsequent*
    // observer mutations and stale-read the initial state.
    let renders = 0
    const { result } = renderHook(
      () => {
        renders += 1
        // Flip the class on the first render so the second StrictMode
        // mount's initializer runs against the new value — but more
        // importantly, the eager `sync()` inside the effect catches any
        // mismatch even if the initializer were to miss it.
        if (renders === 1) {
          document.documentElement.classList.add('dark')
        }
        return useDocumentTheme()
      },
      { wrapper: StrictMode },
    )

    expect(result.current).toBe('dark')
  })
})
