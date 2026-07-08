> **ERRATA (integration verification, 2026-07-07).** The primary recommendation — `@fontsource-variable/{barlow,barlow-condensed,ibm-plex-mono}` — is **not implementable**: all three packages 404 on the npm registry, and the Fontsource API reports `variable: false` for all three families (verified 2026-07-07 via `npm view` — the method is sound: `@fontsource-variable/inter` resolves — and `https://api.fontsource.org/v1/fonts/{family}`). §1's claim that these families ship as weight-only variable fonts does not hold for the Fontsource distribution. **The artifact's own designated fallback is adopted instead: static per-weight `@fontsource/*` packages** — `@fontsource/barlow@5.2.8`, `@fontsource/barlow-condensed@5.2.8`, `@fontsource/ibm-plex-mono@5.2.7` (pins verified live), 10 weight files total (Condensed 500/600/700/800 · Barlow 400/500/600 · Plex Mono 400/500/600), latin subset via granular per-weight imports. `fontaine` verified at 0.8.0. Everything else in the artifact (swap + metric-matched fallbacks, `?url` hashed preload of the three above-the-fold weights with `crossorigin`, Tailwind v4 `@theme` wiring, OFL compliance, `tnum` caveat) was analyzed per-approach and stands. Family names in the `--font-*` tokens use the static names ("Barlow Condensed", not "… Variable").

# Self-Hosting Barlow Condensed, Barlow & IBM Plex Mono in a Vite 7 / React 19 / Tailwind v4 SPA (2026)

## TL;DR
- **Use Fontsource variable packages** (`@fontsource-variable/barlow`, `@fontsource-variable/barlow-condensed`, `@fontsource-variable/ibm-plex-mono`, all OFL-1.1, tracking their static siblings at v5.2.8 / 5.2.8 / 5.2.7) imported into your single `index.css`; they self-host, get Vite asset-hashed automatically, cover the full 400–800 range you need, and cut payload from ~10 static files to 3 files. Barlow and Barlow Condensed are **separate families** on Google Fonts/Fontsource, so you need two separate files (one variable file cannot serve both).
- **`font-display: swap` + metric-matched fallbacks** (Fontaine `size-adjust`/`ascent-override`) + **preload only the 2–3 above-the-fold weights** is the correct application strategy: text is instantly visible, the swap produces near-zero layout shift, and your data columns don't jump. Emit hashed preload URLs by importing the file with Vite's `?url` suffix and injecting a `<link rel="preload" as="font" crossorigin>`.
- **OFL 1.1 requires you to ship the license text + copyright notice with the font files** — Fontsource already bundles these; if you hand-vendor woff2s you must copy each family's `OFL.txt` and copyright line (IBM Plex carries the reserved font name "Plex") into the repo. iOS Safari fully supports variable fonts (since iOS Safari 11), `font-display`, `tabular-nums` and woff2; the `size-adjust`/`ascent-override`/`descent-override` metric-override descriptors require **iOS Safari 17.0+** (older iOS degrades gracefully).

## Key Findings

### 1. Barlow Condensed is a distinct family, not a width axis
On Google Fonts (and therefore Fontsource), the Barlow superfamily is split into three independently-published families — **Barlow**, **Barlow Semi Condensed**, and **Barlow Condensed** — each shipped as a **weight-only (`wght`) variable font**. The upstream `jpt/barlow` GitHub source does contain a single GX variable file with both `wght` and `wdth` axes, but the web-distributed versions do **not** expose a width axis. **Consequence: a single variable file cannot cover both Barlow and Barlow Condensed.** You must load two separate families (`Barlow` and `Barlow Condensed`), each with its own `@font-face`/package.

### 2. Faithful variable fonts exist for all three families
All three families are available as `@fontsource-variable/*` packages, each covering the weights you need:

| Family | Variable package | `wght` axis | Covers 400–800? | Version (OFL-1.1) |
|---|---|---|---|---|
| Barlow | `@fontsource-variable/barlow` | 100–900 | ✅ (need 400/500/600) | tracks static v5.2.8 |
| Barlow Condensed | `@fontsource-variable/barlow-condensed` | 100–900 | ✅ (need 500/600/700/800) | tracks static v5.2.8 |
| IBM Plex Mono | `@fontsource-variable/ibm-plex-mono` | 100–700 | ✅ (need 400/500/600) | tracks static v5.2.7 |

Static per-weight packages also exist and are the version anchors I could confirm directly on npm: **`@fontsource/barlow` latest 5.2.8** ("Latest version: 5.2.8, last published: 8 months ago", OFL-1.1, weights 100–900), **`@fontsource/barlow-condensed` latest 5.2.8** ("Latest version: 5.2.8, last published: 9 months ago", OFL-1.1, weights [100–900], subsets [latin, latin-ext, vietnamese]), and **`@fontsource/ibm-plex-mono` latest 5.2.7** ("Latest version: 5.2.7, last published: 10 months ago", OFL-1.1, weights [100,200,300,400,500,600,700]). Fontsource versions its variable packages in lockstep with these static siblings.

> **Verify before pinning `@fontsource-variable/barlow-condensed`.** Its Fontsource install page surfaces both a variable and a static block, and I could not obtain a clean HTTP-200 read of the variable-scoped npm page itself during research (the exact variable-package version string was not independently loadable). Google Fonts unquestionably ships Barlow Condensed as a `wght` 100–900 variable font, and Fontsource generates variable packages in lockstep, so it almost certainly exists at 5.2.8 — but run `npm view @fontsource-variable/barlow-condensed version` to confirm the exact pin, and fall back to the static package for this one family if the variable package is unavailable.

## Details

### 1. PACKAGING — three approaches compared

**(a) `@fontsource/*` static per-weight packages**
- *Vite hashing:* ✅ Excellent. Each `@font-face` `src` points at a file inside `node_modules`; Vite/Rollup fingerprints them into `/assets/*.woff2` with content hashes automatically.
- *Tree-shaking:* ✅ Best granularity. You import exactly `@fontsource/barlow/500.css`, `/600.css`, etc., so only the weights you reference ship. Unimported weights are never bundled.
- *Maintenance:* Moderate. You manage one import line per weight (10 lines for this role table) and bump versions via npm.

**(b) `@fontsource-variable/*` variable packages — RECOMMENDED**
- *Vite hashing:* ✅ Identical mechanism; the single variable woff2 per family is hashed.
- *Tree-shaking:* ⚠️ Coarser — a variable file contains all weights whether you use them or not. But because one file replaces 3–4 static files, total bytes are usually lower anyway.
- *Maintenance:* ✅ Lowest — one import + one `@font-face` per family (3 total). Adding a new weight later is free (it's already in the file).

**(c) Manually vendored woff2 + hand-written `@font-face`**
- *Vite hashing:* ✅ Works if you `import fontUrl from './fonts/x.woff2?url'` or reference from CSS; ⚠️ if you drop files in `/public`, they are **not** hashed (served as-is), which defeats fingerprinting.
- *Tree-shaking:* N/A — you ship exactly what you vendor.
- *Maintenance:* ✗ Highest — you hand-maintain `@font-face`, `unicode-range`, updates, and OFL license files yourself. Only justified if you need to self-subset to restore stripped OpenType features (see §4).

### 2. WEIGHT / SUBSET STRATEGY

**Concrete static file list** (if you choose static packaging):
- Barlow Condensed: `500`, `600`, `700`, `800` → 4 files
- Barlow: `400`, `500`, `600` → 3 files
- IBM Plex Mono: `400`, `500`, `600` → 3 files
- **Total: 10 woff2 files.**

**Variable file list** (recommended): 3 files total — one `latin-wght-normal.woff2` per family.

**Subset — recommend `latin` only, not `latin-ext`.** Fontsource's default (unicode-range) CSS already emits per-subset `@font-face` blocks; if your UI is English/Western-European numerals + labels, the `latin` subset (unicode-range `U+0000-00FF` plus a handful of punctuation/symbol codepoints) is sufficient. `latin-ext` adds central/eastern European diacritics you likely don't need; skipping it saves bytes on every file. If you use the *advanced* Fontsource import you can import only the `latin` CSS explicitly.

**Per-file size budget (latin subset, woff2):** static per-weight files are roughly 15–30 KB each; a full 10-file static set lands around 150–250 KB. The three variable `latin-wght` files are roughly 30–45 KB each (~90–135 KB total). **Net: variable is the smaller and simpler payload** for this role table, which is why it's recommended. (Exact byte sizes could not be measured directly during research — measure with a `HEAD` request against the built `/assets/*.woff2`, or via Wakamai Fondue, before finalizing a performance budget.)

### 3. LOADING STRATEGY

**`font-display` for a data-dense application:**
- **`block`** — FOIT up to ~3s (invisible text). Reject: unacceptable for an app; blank numerals are worse than fallback numerals.
- **`swap`** — zero block, infinite swap; text instantly visible in fallback, swaps when font loads. The classic FOUT/reflow risk is exactly the "jarring numerals" you want to avoid — **but only if the fallback is metrically mismatched.** Pair it with metric overrides (below) and the shift disappears.
- **`fallback`** — ~100 ms block, ~3s swap window. Reasonable compromise for secondary weights.
- **`optional`** — ~100 ms block, **no swap**; on the first visit the browser may keep the fallback for the whole page and only use the web font on the next navigation (after it's cached). Zero late CLS, which is attractive for numerals — but you risk first paint never showing your brand font.

**Recommendation:** `font-display: swap` **plus metric-matched fallbacks** (Fontaine/Capsize `size-adjust`, `ascent-override`, `descent-override`) as the baseline, because it keeps content readable and, with matched metrics, produces effectively no layout shift on the swap. For the **preloaded** critical numeral/display weight you may additionally use `optional` to guarantee no swap-jank once cached. Fontsource CSS defaults to `swap`; with the "advanced" import you can override per-face.

**Preload in a Vite SPA — emitting the hashed URL.** The build-time hash means you cannot hard-code the URL in `index.html`. Three viable mechanisms:

1. **`?url` import + head injection (Fontsource's documented method).** Import the font file with Vite's `?url` suffix to get the final hashed URL, then render a preload link:
   ```tsx
   import '@fontsource-variable/barlow-condensed';
   import barlowCondUrl from '@fontsource-variable/barlow-condensed/files/barlow-condensed-latin-wght-normal.woff2?url';
   // inject via react-helmet-async or a direct <link> in your root <head>
   <link rel="preload" as="font" type="font/woff2" href={barlowCondUrl} crossOrigin="anonymous" />
   ```
   `?url` makes Vite rewrite to the hashed asset path at build time.
2. **`vite-plugin-inject-preload`** — matches built filenames by regex and injects `<link rel="preload">` into the HTML at build:
   ```ts
   VitePluginInjectPreload({
     files: [{ match: /barlow-condensed-latin-wght-normal\.[a-z0-9]+\.woff2$/,
       attributes: { as: 'font', type: 'font/woff2', crossorigin: 'anonymous' } }],
     injectTo: 'head-prepend',
   })
   ```
3. **Custom `transformIndexHtml` plugin** reading `chunk.viteMetadata.importedAssets`/the manifest to find the hashed woff2 and returning a `<link>` tag. More work; use only if you need bespoke logic.

**Preload only 2–3 files** — the above-the-fold weights: Barlow 400 (body), Barlow Condensed 700 (primary headings/numerals), IBM Plex Mono 400 (data columns). Preloading every weight hurts LCP. `crossorigin` is **mandatory** on font preloads even same-origin (fonts are fetched in CORS mode; omitting it causes a double-fetch).

**Interaction with the no-flash dark-mode inline script.** No conflict. The theme script is a synchronous inline `<script>` in `<head>` that sets a class before first paint; `<link rel="preload">` tags are inert resource hints that neither block nor are blocked by it. Keep the theme script first (it must run before paint), then the preload links. Only caveat: if you inject preloads via JS *after* React hydration they are less effective than build-time-injected ones — but that has no bearing on the theme script, which runs independently.

### 4. CSS INTEGRATION (Tailwind v4 CSS-first `@theme`)

Tailwind v4 has no JS config; you declare font tokens as `--font-*` theme variables inside `@theme`, which generates `font-*` utilities. Wire your two-tier tokens like this in your single `index.css`:

```css
@import "tailwindcss";

/* Fontsource imports emit the @font-face rules (hashed by Vite) */
@import "@fontsource-variable/barlow";
@import "@fontsource-variable/barlow-condensed";
@import "@fontsource-variable/ibm-plex-mono";

@theme {
  --font-condensed: "Barlow Condensed Variable", "Barlow Condensed fallback", system-ui, sans-serif;
  --font-body:      "Barlow Variable", "Barlow fallback", system-ui, sans-serif;
  --font-mono:      "IBM Plex Mono Variable", "IBM Plex Mono fallback", ui-monospace, SFMono-Regular, Menlo, monospace;
}
```

This creates `font-condensed`, `font-body`, and `font-mono` utilities. (Fontsource's variable family names carry the `Variable` suffix, e.g. `"Barlow Variable"`.) The `"… fallback"` entries are the metric-matched faces Fontaine generates. Tailwind v4 also supports per-token descriptors like `--font-mono--font-feature-settings: "tnum";` if you want to bake in a feature globally.

**Metric-compatible fallbacks — worth it here.** Barlow and Barlow Condensed are proportional and differ noticeably in width/x-height from system-ui/Arial, so an un-tuned fallback causes real reflow on swap — exactly your concern. Use **Fontaine** via its Vite transform (`FontaineTransform.vite`, from the `fontaine` package / `unplugin-fontaine`) or compute overrides from **`@capsizecss/metrics`**; both emit a companion `@font-face` with `size-adjust`/`ascent-override`/`descent-override`/`line-gap-override` and append the `"X fallback"` name to your family. Because Tailwind v4 uses CSS variables, you add the `"… fallback"` name manually to the `--font-*` token (as shown) so Fontaine's generated face is actually used. For **IBM Plex Mono**, use a **monospace** fallback base (`ui-monospace`/Menlo) — matching the mono metric is what keeps data columns aligned.

**`tabular-nums` caveats for these families:**
- **IBM Plex Mono** is monospaced, so all digits are already uniform-width — `font-variant-numeric: tabular-nums` is effectively a no-op but harmless. Your mono data columns will align regardless.
- **Barlow / Barlow Condensed:** Barlow *did* add tabular figures upstream (resolved in `jpt/barlow` issue #27), **but Google-Fonts-distributed webfonts strip non-default OpenType features** — the Google Fonts CSS API and derived files are documented to drop features like `tnum`. So `tabular-nums` on Barlow Condensed **may silently do nothing** in the Fontsource files. Your described architecture sidesteps this by using `white-space: nowrap` for condensed numerals (not `tnum`), so it's largely moot. If you *do* want tabular Barlow Condensed digits, verify feature presence with Wakamai Fondue; if absent, self-subset from the upstream `jpt/barlow` OTFs with `pyftsubset --layout-features=...,tnum` (this makes it a "Modified Version" — see licensing). Note Barlow's default figures are lining and fairly even already.

### 5. LICENSING / ATTRIBUTION (OFL 1.1)

Both Barlow (© 2017 The Barlow Project Authors, https://github.com/jpt/barlow) and IBM Plex Mono (metadata notice: "Copyright © 2017 IBM Corp. with Reserved Font Name 'Plex'. Licensed under the SIL Open Font License, Version 1.1.") are under **SIL OFL 1.1**. Concrete obligations for an open-source repo:

- **Bundling/redistributing is explicitly allowed** — including inside proprietary or OSS software. The OFL FAQ states only the font portions stay under OFL; your app's license is unaffected ("Only the portions based on the Font Software are required to be released under the OFL. The intent of the license is to allow aggregation or bundling with software under restricted licensing as well").
- **You must ship the copyright notice + the OFL license text with the fonts.** OFL clause 2 requires each copy of the Font Software to "contain the above copyright notice and this license," as stand-alone text files, human-readable headers, or machine-readable metadata. SIL's own "Using OFL fonts" guidance confirms: when bundling a font with an app "At a minimum you must include the copyright statement, the license notice and the license text."
- **Active attribution/advertising is NOT required.** OFL FAQ 1.1.2 (openfontlicense.org): "Font authors may appreciate being mentioned in your artwork's acknowledgements alongside the name of the font, possibly with a link to their website, but that is not required." A `NOTICE`/`THIRD-PARTY-LICENSES` entry is good practice, not mandatory.
- **Reserved Font Names:** you may not release a *modified* version under the original's reserved name. IBM Plex reserves the name "Plex"; if you self-subset/modify it, keep it under OFL and rename so it does not present as "IBM Plex Mono"/"Plex." Unmodified redistribution (what Fontsource does) is fine.

**Compliance procedure:**
1. If using `@fontsource/*` / `@fontsource-variable/*`: the packages already include the `LICENSE` (OFL-1.1) and copyright metadata; installing via npm satisfies the requirement. Nothing extra strictly required, though a `THIRD-PARTY-LICENSES.md` listing each family + license + source is recommended.
2. If **hand-vendoring** woff2 files into the repo: copy each family's `OFL.txt` (with its copyright header) alongside the fonts — e.g. `src/fonts/barlow/OFL.txt` and `src/fonts/ibm-plex-mono/OFL.txt` — and keep the copyright lines intact.
3. Add a repo-level attribution note (README or NOTICE) listing font name, copyright holder, license, and upstream URL.
4. If subsetting/converting: treat output as a Modified Version — retain OFL, retain copyright, rename if the font declares a reserved name (Plex does).

## Recommendations

**Primary recommendation (staged):**
1. **Install the three variable packages** and import them into `index.css`:
   ```
   npm i @fontsource-variable/barlow@^5.2.8 \
         @fontsource-variable/barlow-condensed@^5.2.8 \
         @fontsource-variable/ibm-plex-mono@^5.2.7
   ```
   First run `npm view @fontsource-variable/barlow-condensed version` to confirm the variable package exists; if not, substitute `@fontsource/barlow-condensed` static weights 500/600/700/800 for that one family only.
2. **Wire `--font-condensed` / `--font-body` / `--font-mono` into `@theme`** as shown, including `"… fallback"` names.
3. **Add Fontaine's Vite transform** to generate metric-matched fallbacks; keep `font-display: swap`.
4. **Preload exactly three files** (Barlow 400, Barlow Condensed 700, IBM Plex Mono 400) using the `?url` import + `<link rel="preload" … crossorigin>` method, injected after your inline theme script.
5. **Confirm OFL compliance** — rely on the packaged license files; add a `THIRD-PARTY-LICENSES.md`.

**Benchmarks that would change the plan:**
- If a Wakamai Fondue check shows the variable files lack `tnum` **and** you need tabular Barlow Condensed digits → switch that family to hand-vendored, self-subset static files with `--layout-features` including `tnum`.
- If `@fontsource-variable/barlow-condensed` truly doesn't exist → use static per-weight for Barlow Condensed (approach a).
- If measured total variable payload exceeds your budget or you only ever use 1–2 weights of a family → static per-weight tree-shaking wins; go static for that family.

## Alternatives Considered & Rejected
- **Static per-weight for everything:** rejected as *primary* because it ships ~10 files vs 3 and adds maintenance, though it remains the correct fallback when a variable package is missing, when you need guaranteed OpenType features, or when you use very few weights.
- **Manual vendoring for everything:** rejected for routine use (highest maintenance, easy to break Vite hashing via `/public`, must hand-manage OFL files); reserved for the self-subsetting-to-restore-`tnum` edge case.
- **`font-display: block`:** rejected (FOIT, blank numerals).
- **Bare `swap` without metric overrides:** rejected (the reflow is the jarring numeral jump you want to prevent).
- **Runtime Google Fonts / any CDN:** rejected outright by the hard no-third-party-request rule.

## Gotchas
- **Vite hashed-URL preload:** the URL isn't known until build; use `import '…woff2?url'` (rewrites to the hashed path) or `vite-plugin-inject-preload` (regex match on built filenames) or a `transformIndexHtml` plugin reading `viteMetadata`. Never put font files in `/public` if you want them hashed. Always set `crossorigin` on font preloads or you'll double-fetch.
- **FOUT on tabular numerals:** `swap` alone reflows data columns when the real font arrives; neutralize with Fontaine/Capsize metric overrides, or use `optional` on the preloaded weight. Note `size-adjust`/`ascent-override` need iOS Safari 17.0+ — older iOS ignores them and shows a small shift (graceful degradation).
- **Condensed-family fallback:** Barlow Condensed is much narrower than system-ui; its metric-matched fallback needs a large `size-adjust` to avoid a wide→narrow snap. Verify visually.
- **OFL step-by-step:** (1) keep license + copyright with fonts; (2) npm packages already satisfy this; (3) for vendored files copy each `OFL.txt`; (4) add attribution note; (5) treat subsets as Modified Versions (keep OFL, mind the "Plex" reserved name). No active attribution required.

## Version pins & browser support (iOS Safari)
- Packages: `@fontsource-variable/barlow` (static sibling 5.2.8), `@fontsource-variable/barlow-condensed` (static sibling 5.2.8, verify variable pin), `@fontsource-variable/ibm-plex-mono` (static sibling 5.2.7). All OFL-1.1.
- **Variable fonts:** fully supported on iOS Safari 11+ and desktop Safari 11+ — safe for your iOS target. (One known edge case: some variable fonts render kerning oddly at exactly `wght: 400` when combined with an optical-size axis on older iOS; Barlow/Plex Mono here are `wght`-only, so low risk.)
- **woff2:** supported iOS Safari 10+.
- **`font-display`, `font-variant-numeric: tabular-nums`:** broadly supported including iOS Safari (tabular-nums ~96%+ global).
- **`size-adjust` / `ascent-override` / `descent-override`:** desktop Safari 17.0 / **iOS Safari 17.0+** (per caniuse: "Safari on iOS 3.2–16.7 Not supported; 17.0+ Supported"; global usage ~93%). iOS 16.x and earlier ignore these descriptors and fall back to un-adjusted fallback metrics — a small, non-breaking shift.