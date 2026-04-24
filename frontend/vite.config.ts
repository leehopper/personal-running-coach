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
      '~': path.resolve(__dirname, './src/app'),
    },
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
