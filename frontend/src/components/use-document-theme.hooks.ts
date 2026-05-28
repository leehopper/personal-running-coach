import { useEffect, useState } from 'react'

// shadcn's Sonner integration assumes `next-themes`, but this is a Vite SPA
// with its own ThemeProvider that toggles a `.dark` class on
// `documentElement` (matched by an inline no-flash script in `index.html`).
// Rather than pulling in `next-themes` just so the toaster can pick a colour
// scheme, this hook mirrors that class directly: it reads the current value
// synchronously during render and subscribes to subsequent changes via a
// `MutationObserver` so any consumer stays in sync with theme toggles.
export function useDocumentTheme(): 'light' | 'dark' {
  const [theme, setTheme] = useState<'light' | 'dark'>(() =>
    typeof document !== 'undefined' && document.documentElement.classList.contains('dark')
      ? 'dark'
      : 'light',
  )

  useEffect(() => {
    const root = document.documentElement
    const sync = () => setTheme(root.classList.contains('dark') ? 'dark' : 'light')
    // Eagerly resync before subscribing: the class can change between the
    // render-phase initializer and effect mount (StrictMode remount, an
    // external mutator running during commit, JSDOM test setup), and without
    // this the observer would only see *subsequent* mutations.
    sync()
    const observer = new MutationObserver(sync)
    observer.observe(root, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [])

  return theme
}
