import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// Vite dev server runs on HTTPS (port 5173) and proxies `/api/*` to the
// ASP.NET Core API on https://localhost:5001.
//
// HTTPS on the dev server is a hard requirement: the auth cookies use the
// `__Host-` prefix, which browsers silently drop over plain HTTP. See
// CONTRIBUTING.md "Local HTTPS is required" for the full contract.
//
// `@vitejs/plugin-basic-ssl` generates a self-signed cert at startup — zero
// install, one-time browser accept. Contributors who want a trusted cert
// use the mkcert escape hatch documented in CONTRIBUTING.md.
//
// The `/api` proxy preserves the `__Host-` cookie contract:
//   - `secure: false` accepts the ASP.NET Core dev cert (self-signed).
//   - `changeOrigin: true` rewrites the Host header so backend CORS /
//     antiforgery see the target origin, not the Vite origin.
//   - `cookieDomainRewrite: ''` strips any `Domain=` attribute from upstream
//     Set-Cookie headers. The backend does not set one today, so this is a
//     belt-and-suspenders guard — `__Host-` prefix rejects cookies that
//     carry a Domain attribute.
export default defineConfig({
  plugins: [react(), basicSsl(), tailwindcss()],
  resolve: {
    alias: {
      '~': path.resolve(__dirname, './src/app'),
    },
  },
  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
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
  },
})
