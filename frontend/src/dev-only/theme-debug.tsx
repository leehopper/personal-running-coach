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

// Tailwind v4 (Oxide) is a static source scanner — interpolated
// arbitrary-value class strings (e.g. `bg-[var(--${token})]`) are never
// seen as full literals by the scanner and therefore never emitted into
// the CSS bundle. Each entry must carry the full, literal Tailwind utility
// class string so the scanner can collect it at build time.

interface TokenPair {
  fill: string
  on: string
  label: string
}

// Each pair fills with the semantic background colour and writes its own
// label in the matching foreground colour so contrast is visible at a glance.
const TOKEN_PAIRS: ReadonlyArray<TokenPair> = [
  { fill: 'bg-background', on: 'text-foreground', label: 'background / foreground' },
  { fill: 'bg-card', on: 'text-card-foreground', label: 'card / card-foreground' },
  { fill: 'bg-popover', on: 'text-popover-foreground', label: 'popover / popover-foreground' },
  { fill: 'bg-primary', on: 'text-primary-foreground', label: 'primary / primary-foreground' },
  {
    fill: 'bg-secondary',
    on: 'text-secondary-foreground',
    label: 'secondary / secondary-foreground',
  },
  { fill: 'bg-muted', on: 'text-muted-foreground', label: 'muted / muted-foreground' },
  { fill: 'bg-accent', on: 'text-accent-foreground', label: 'accent / accent-foreground' },
  {
    fill: 'bg-destructive',
    on: 'text-destructive-foreground',
    label: 'destructive / destructive-foreground',
  },
]

interface LineToken {
  border: string
  label: string
}

// Single-role tokens — rendered as a labelled border/outline sample.
// `border-border`, `border-input`, `ring-ring` are the semantic utilities
// exposed by the `@theme inline` block; they must appear as full literal
// strings so the scanner emits them.
const LINE_TOKENS: ReadonlyArray<LineToken> = [
  { border: 'border-border', label: 'border' },
  { border: 'border-input', label: 'input' },
  { border: 'ring-ring', label: 'ring' },
]

interface SwatchProps {
  fill: string
  on: string
  label: string
  name: string
}

const Swatch = ({ fill, on, label, name }: SwatchProps) => (
  <div
    data-testid={`swatch-${name}`}
    className={`flex min-h-20 flex-col justify-between rounded-lg border border-border p-3 ${fill} ${on}`}
  >
    <span className="text-sm font-medium">{label.split(' / ')[0]}</span>
    <span className="text-xs opacity-80">on {label.split(' / ')[1]}</span>
  </div>
)

interface LineSwatchProps {
  border: string
  label: string
}

const LineSwatch = ({ border, label }: LineSwatchProps) => (
  <div
    data-testid={`swatch-${label}`}
    className={`flex min-h-20 items-center justify-center rounded-lg bg-background p-3 border-[3px] border-solid ${border}`}
  >
    <span className="text-sm font-medium text-foreground">--{label}</span>
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
        {TOKEN_PAIRS.map(({ fill, on, label }) => (
          <Swatch key={fill} fill={fill} on={on} label={label} name={label.split(' / ')[0]} />
        ))}
        {LINE_TOKENS.map(({ border, label }) => (
          <LineSwatch key={label} border={border} label={label} />
        ))}
      </section>
    </main>
  )
}
