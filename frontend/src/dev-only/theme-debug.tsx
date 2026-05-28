// Dev-only design-token inspector — dead-code-eliminated in production.
// `app.component.tsx` registers the `/dev/theme-debug` route inside an
// `import.meta.env.DEV &&` gate; Vite replaces that with the literal
// `false` for `build`, so the route element is never constructed and
// Rollup tree-shakes this whole module out (verified: `grep -r
// theme-debug dist/` returns no matches). Every consumer in this
// directory must gate its import/usage with `import.meta.env.DEV`.
//
// The page renders a swatch for every shadcn semantic token so the token
// layer (T01.2) and the dark cascade (T01.3) can be eyeballed in
// isolation, and drives the mode toggle through the real ThemeProvider.

import { useTheme } from '@/components/theme-context'

// Each pair is a [fill, text-on-fill] token duo — the swatch fills with
// the first and writes its own name with the second so contrast is
// visible at a glance.
const TOKEN_PAIRS: ReadonlyArray<readonly [fill: string, on: string]> = [
  ['background', 'foreground'],
  ['card', 'card-foreground'],
  ['popover', 'popover-foreground'],
  ['primary', 'primary-foreground'],
  ['secondary', 'secondary-foreground'],
  ['muted', 'muted-foreground'],
  ['accent', 'accent-foreground'],
  ['destructive', 'destructive-foreground'],
]

// Single-role tokens — rendered as a labelled border/outline sample.
const LINE_TOKENS = ['border', 'input', 'ring'] as const

const Swatch = ({ fill, on }: { fill: string; on: string }) => (
  <div
    data-testid={`swatch-${fill}`}
    className={`flex min-h-20 flex-col justify-between rounded-lg border border-border p-3 bg-[var(--${fill})] text-[var(--${on})]`}
  >
    <span className="text-sm font-medium">--{fill}</span>
    <span className="text-xs opacity-80">on --{on}</span>
  </div>
)

const LineSwatch = ({ token }: { token: string }) => (
  <div
    data-testid={`swatch-${token}`}
    className={`flex min-h-20 items-center justify-center rounded-lg bg-background p-3 border-[3px] border-solid border-[color:var(--${token})]`}
  >
    <span className="text-sm font-medium text-foreground">--{token}</span>
  </div>
)

export const ThemeDebugPage = (): React.ReactElement => {
  const { theme, resolvedTheme, setTheme } = useTheme()

  return (
    <main className="min-h-screen bg-background p-6 text-foreground">
      <header className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Theme debug</h1>
          <p className="text-sm text-muted-foreground">
            Semantic design tokens — choice: {theme}, resolved: {resolvedTheme}
          </p>
        </div>
        <div role="group" aria-label="Theme mode" className="flex gap-2">
          {(['light', 'dark', 'system'] as const).map((mode) => (
            <button
              key={mode}
              type="button"
              onClick={() => setTheme(mode)}
              aria-pressed={theme === mode}
              className="rounded-md border border-border bg-card px-3 py-1 text-sm text-card-foreground transition-colors duration-200 ease-out hover:bg-accent hover:text-accent-foreground motion-reduce:transition-none aria-pressed:bg-primary aria-pressed:text-primary-foreground"
            >
              {mode}
            </button>
          ))}
        </div>
      </header>

      <section
        aria-label="Semantic token swatches"
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
      >
        {TOKEN_PAIRS.map(([fill, on]) => (
          <Swatch key={fill} fill={fill} on={on} />
        ))}
        {LINE_TOKENS.map((token) => (
          <LineSwatch key={token} token={token} />
        ))}
      </section>
    </main>
  )
}
