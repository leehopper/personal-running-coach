import { configDefaults, defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import fs from 'node:fs'
import path from 'node:path'

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

export default defineConfig({
  plugins: [react(), ...(mkcertPresent ? [] : [basicSsl()]), tailwindcss()],
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
      ],
    },
  },
})
