import { configDefaults, defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import { generateFontFace, getMetricsForFamily } from 'fontaine'
import fs from 'node:fs'
import path from 'node:path'
import type { Plugin } from 'vite'
import { FONT_FALLBACK_SPECS, matchPreloadFiles } from './scripts/font-build.helpers'

// Vite dev server runs on HTTPS (port 5173) and proxies `/api/*` to the
// ASP.NET Core API on https://localhost:5001.
//
// HTTPS on the dev server is a hard requirement: the auth cookies use the
// `__Host-` prefix, which browsers silently drop over plain HTTP. See
// CONTRIBUTING.md "Local HTTPS is required" for the full contract.
//
// Two cert strategies, picked at config time:
//   1. If `.cert/cert.pem` + `.cert/key.pem` are present (mkcert output),
//      the dev server serves them directly via `server.https` — browser
//      padlock is green, no "Not Secure" chip.
//   2. Otherwise `@vitejs/plugin-basic-ssl` generates a fresh self-signed
//      cert at startup — zero install, one-time browser accept, CI-safe.
// CONTRIBUTING.md has the mkcert recipe for contributors who want the
// trusted-cert path.
//
// The `/api` proxy preserves the `__Host-` cookie contract:
//   - `secure: false` accepts the ASP.NET Core dev cert (self-signed).
//   - `changeOrigin: true` rewrites the Host header so backend CORS /
//     antiforgery see the target origin, not the Vite origin.
//   - `cookieDomainRewrite: ''` strips any `Domain=` attribute from upstream
//     Set-Cookie headers. The backend does not set one today, so this is a
//     belt-and-suspenders guard — `__Host-` prefix rejects cookies that
//     carry a Domain attribute.
const mkcertCertPath = path.resolve(__dirname, '.cert/cert.pem')
const mkcertKeyPath = path.resolve(__dirname, '.cert/key.pem')
const mkcertPresent = fs.existsSync(mkcertCertPath) && fs.existsSync(mkcertKeyPath)
const httpsConfig = mkcertPresent
  ? { cert: fs.readFileSync(mkcertCertPath), key: fs.readFileSync(mkcertKeyPath) }
  : undefined

// Self-hosted Alpine typography — metric-matched fallback faces so that
// `font-display: swap` does not reflow the layout when the real webfont
// finishes loading (a system fallback with mismatched metrics would shift
// every line on swap — visible CLS). This plugin appends, per self-hosted
// family, a fontaine-computed `@font-face` fallback rule carrying
// `size-adjust`/`ascent-override`/`descent-override`/`line-gap-override`
// tuned so the fallback occupies the same box as the real font; the
// `--font-*` tokens in src/index.css already reference each `"<family>
// fallback"` name so the fallback face is actually used during swap.
//
// It does NOT use `FontaineTransform.vite()` (fontaine's own automatic
// per-module @font-face scanner, the documented integration), because that
// produces WRONG output in this build. `@tailwindcss/vite` resolves and
// inlines src/index.css's entire `@import` graph — including every
// @fontsource import — as part of its own 'pre'-enforced transform. In a
// PRODUCTION build that inlined output then goes through Tailwind's CSS
// minify pass, which drops quotes around any multi-word `font-family` value
// that doesn't strictly need them (`'Barlow Condensed'` -> `Barlow
// Condensed`, valid CSS). (In dev the quotes are preserved — this is a
// production-only mangling.) fontaine's family-name parser (css-tree based)
// reads only the FIRST identifier token of an unquoted, multi-word value, so
// on a production build it extracts "Barlow" from "Barlow Condensed"
// (colliding with the real Barlow family — Barlow Condensed's fallback then
// silently reuses Barlow's wider metrics) and "IBM" from "IBM Plex Mono" (an
// unnamed fallback that nothing references). No plugin ordering avoids it:
// running fontaine before `tailwindcss()` shows it the raw un-inlined
// `@import` statements with zero `@font-face` rules to find.
//
// So this reuses fontaine's own metric-computation utilities
// (`getMetricsForFamily` + `generateFontFace`, the exact functions its
// scanner calls internally) but drives them from an explicit list of KNOWN
// family names with quote-agnostic regexes that match whether or not the
// production minify pass stripped the quotes.
//
// It is a plain Vite plugin using the OBJECT form of the `transform` hook —
// `transform: { filter, handler }` — which Vite 8's Rolldown-backed pipeline
// runs and lets mutate the module during `vite build` (verified by grepping
// the three fallback faces out of `dist/assets/*.css`). The bare FUNCTION
// form `transform(code, id)` is what does not reliably fire here; the
// object-with-filter form does, so no wrapper library is needed.
function fontFallbackFaces(): Plugin {
  return {
    name: 'font-fallback-faces',
    transform: {
      filter: { id: [/index\.css$/] },
      async handler(code: string) {
        let output = code
        let changed = false
        for (const { family, systemFallback } of FONT_FALLBACK_SPECS) {
          const fallbackFamily = `${family} fallback`
          // Only matches an actual @font-face declaration whose ENTIRE
          // font-family value is the fallback name — NOT the "Barlow
          // fallback"/etc. substrings that also appear (correctly, as part
          // of a longer comma-separated stack) inside the --font-* custom
          // property tokens declared elsewhere in this same file.
          const alreadyGenerated = new RegExp(
            `font-family:\\s*["']${fallbackFamily}["']\\s*;`,
          ).test(output)
          if (alreadyGenerated) continue
          const hasRealFace = new RegExp(`font-family:\\s*["']?${family}["']?\\s*[;}]`).test(output)
          if (!hasRealFace) continue
          // Past the hasRealFace guard, the real @font-face IS present, so a
          // null metrics lookup means "we can't compute this family's
          // metric-matched fallback." Failing soft here would ship a
          // --font-* token pointing at a "<family> fallback" name that has
          // no @font-face rule — the browser would drop straight to system
          // fonts on swap, reintroducing exactly the layout shift (CLS) this
          // plugin exists to prevent, with nothing catching it. Fail loud,
          // mirroring preloadFontLinks()'s contract.
          const metrics = await getMetricsForFamily(family)
          if (!metrics) {
            throw new Error(
              `font-fallback-faces: no font metrics found for real family "${family}". ` +
                `Its @font-face rule is present but fontaine's metrics collection has no entry ` +
                `for it — cannot generate a metric-matched fallback face.`,
            )
          }
          const fallbackMetrics = await getMetricsForFamily(systemFallback)
          if (!fallbackMetrics) {
            throw new Error(
              `font-fallback-faces: no font metrics found for system fallback "${systemFallback}" ` +
                `(configured for family "${family}"). Pick a fallback that exists in fontaine's ` +
                `metrics collection (e.g. Arial for sans, Courier New for monospace).`,
            )
          }
          output += generateFontFace(metrics, {
            name: fallbackFamily,
            font: systemFallback,
            metrics: fallbackMetrics,
          })
          changed = true
        }
        return changed ? output : null
      },
    },
  }
}

// Injects `<link rel="preload" as="font" crossorigin>` for the three
// above-the-fold weights (see PRELOAD_FONT_PATTERNS in the helper module)
// into the built dist/index.html, placed right before `</head>` — i.e.
// after the no-flash theme `<script>`, which is already in `<head>` ahead
// of this insertion point. `crossorigin` is mandatory even same-origin:
// fonts are fetched in CORS mode, and a preload without it simply
// double-fetches. The hashed asset filename isn't known until the bundle
// is generated, so this reads the final bundle via `transformIndexHtml`'s
// `order: 'post'` hook (only populated during `vite build`, not `vite dev`)
// rather than hardcoding a path; the alternative `?url` import + JSX
// injection approach would need a runtime React component, whereas this
// build-time approach needs no runtime code and guarantees the tags are in
// the static HTML for the very first request.
function preloadFontLinks(): Plugin {
  // Captured from `configResolved` so the injected hrefs honour a non-root
  // Vite `base` (e.g. a sub-path deployment). Vite re-bases its own
  // injected `<script>`/`<link>` tags but not ones we add here, so without
  // this the three preload links would 404 under any base other than '/'.
  let base = '/'
  return {
    name: 'preload-font-links',
    apply: 'build',
    configResolved(config) {
      base = config.base
    },
    transformIndexHtml: {
      order: 'post',
      handler(html, ctx) {
        const bundle = ctx.bundle
        if (!bundle) return html

        // Throws (fail-loud) if any required weight is missing from the
        // build output — a missing preload is a silent LCP regression.
        const fileNames = matchPreloadFiles(Object.keys(bundle))
        // Join base + filename with exactly one slash between them, whatever
        // the base's trailing slash (default '/' -> "/assets/…"; "/app/" ->
        // "/app/assets/…"; "/app" -> "/app/assets/…").
        const basePrefix = base.endsWith('/') ? base : `${base}/`
        const links = fileNames
          .map(
            (fileName) =>
              `<link rel="preload" as="font" type="font/woff2" href="${basePrefix}${fileName}" crossorigin />`,
          )
          .join('\n    ')
        // Consume any indentation already preceding `</head>` so the
        // injected block always lands at a clean, consistent 4-space
        // indent regardless of what Vite's own html transforms (module
        // script, modulepreload, stylesheet — all injected earlier in the
        // `transformIndexHtml` chain) left before the closing tag. Plain
        // index/slice (not a regex) — a leading `[ \t]*` before a literal
        // trips sonarjs/super-linear-regex.
        const headCloseIndex = html.indexOf('</head>')
        if (headCloseIndex === -1) return html
        let indentStart = headCloseIndex
        while (
          indentStart > 0 &&
          (html[indentStart - 1] === ' ' || html[indentStart - 1] === '\t')
        ) {
          indentStart -= 1
        }
        return (
          html.slice(0, indentStart) +
          `    ${links}\n  </head>` +
          html.slice(headCloseIndex + '</head>'.length)
        )
      },
    },
  }
}

export default defineConfig({
  plugins: [
    react(),
    ...(mkcertPresent ? [] : [basicSsl()]),
    tailwindcss(),
    fontFallbackFaces(),
    preloadFontLinks(),
  ],
  resolve: {
    alias: {
      // `@` is shadcn/ui's canonical alias — it expects `@/components/ui`,
      // `@/lib/utils`, etc. Maps to `src/` (not `src/app/`, which is `~`).
      '@': path.resolve(__dirname, './src'),
      '~': path.resolve(__dirname, './src/app'),
      // `~dev-only` reaches the build-time-stripped harness directory.
      // Everything under `src/dev-only/` is gated by `import.meta.env.DEV`
      // and tree-shaken to zero bytes in production (DEC-068 §10.5).
      '~dev-only': path.resolve(__dirname, './src/dev-only'),
    },
  },
  build: {
    rollupOptions: {
      output: {
        // Isolate OTel into its own long-cached chunk (DEC-069 / R-074 §6).
        // OTel changes far less frequently than app code, so splitting it
        // out lets the browser cache it across normal deploys. Bundle
        // delta target is 30–45 KB gz for this chunk; the build summary
        // surfaces the exact size on every `npm run build`. Function form
        // (rather than the object form) sidesteps a Rollup typings quirk
        // where the bare object literal is narrowed to `ManualChunksFunction`
        // and rejected; function form is canonical and gives a stable
        // single-chunk grouping for any module ID under `@opentelemetry/`.
        manualChunks: (id: string): string | undefined => {
          if (id.includes('node_modules/@opentelemetry/')) return 'otel'
          return undefined
        },
      },
    },
    // Default 500 KB is too loose for our footprint — drop to 100 KB so a
    // future regression in the otel chunk (or any other chunk) lights up
    // the build summary immediately. The main JS chunk is currently
    // ~140 KB gz, ~450 KB raw, so it will warn until we code-split the
    // route tree (separate slice).
    chunkSizeWarningLimit: 100,
  },
  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
    https: httpsConfig,
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        secure: false,
        changeOrigin: true,
        cookieDomainRewrite: '',
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    // jsdom URL mirrors the Vite dev server so relative API paths (e.g.
    // `/api/...`) resolve cleanly inside RTK Query / fetch, and so
    // `document.cookie` interactions against `__Host-`-prefixed cookies
    // behave like they do against the real dev server.
    environmentOptions: {
      jsdom: { url: 'https://localhost:5173/' },
    },
    setupFiles: './src/test-setup.ts',
    css: true,
    // `e2e/` holds Playwright specs that import `@playwright/test` and
    // drive a real browser; Vitest must leave them alone.
    exclude: [...configDefaults.exclude, 'e2e/**'],
    coverage: {
      provider: 'v8',
      // `lcov` feeds SonarCloud (sonar-project.properties points at
      // `coverage/lcov.info`); `text` keeps a readable summary in CI logs;
      // `html` stays useful locally for drilling into uncovered lines.
      reporter: ['text', 'lcov', 'html'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.spec.{ts,tsx}',
        'src/**/*.test.{ts,tsx}',
        'src/main.tsx',
        'src/test-setup.ts',
        'src/vite-env.d.ts',
        // shadcn/ui primitives + cn() helper are vendor-copy code per
        // the shadcn convention; mirror sonar.coverage.exclusions so
        // the local report matches what SonarCloud measures.
        'src/components/ui/**',
        'src/lib/utils.ts',
      ],
    },
  },
})
