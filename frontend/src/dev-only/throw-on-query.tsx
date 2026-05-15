// Dead-code-eliminated in production. Vite statically replaces
// `import.meta.env.DEV` with the literal `false` for `build`, so the
// module body collapses to `return null` and Rollup tree-shakes the
// component out of the bundle entirely (verified via
// `grep -r 'throw-on-query' dist/` returning no matches).
//
// Convention: every consumer of files under `frontend/src/dev-only/` must
// gate the import or usage with `import.meta.env.DEV`. The directory exists
// solely to give Playwright a deterministic throw-on-demand seam without
// dragging a forcing-throw component into the production tree.

export const ThrowOnQuery = (): null => {
  if (!import.meta.env.DEV) return null

  const params = new URLSearchParams(window.location.search)
  const mode = params.get('throw')
  if (mode === 'render') {
    throw new Error('ThrowOnQuery: forced render-time throw for E2E test')
  }
  return null
}
