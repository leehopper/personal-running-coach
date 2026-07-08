# Third-Party Font Licenses

RunCoach self-hosts the following typefaces (Slice 0 / DR-6, `frontend/src/index.css`).
Each is distributed under the **SIL Open Font License, Version 1.1** (OFL-1.1) and
bundled as-is via the npm `@fontsource/*` packages — no modification, subsetting, or
re-encoding is performed. The npm packages already carry each family's `LICENSE` file
and copyright metadata; this document is the repository-level attribution note the
OFL recommends (not strictly required) for bundled fonts.

## Barlow

- **Copyright:** © 2017 The Barlow Project Authors
- **License:** SIL Open Font License, Version 1.1 (OFL-1.1)
- **Upstream:** https://github.com/jpt/barlow
- **npm packages:** `@fontsource/barlow@5.2.8`, `@fontsource/barlow-condensed@5.2.8`
  (Barlow and Barlow Condensed are independently-published families from the same
  upstream project; both carry the same copyright notice.)

## IBM Plex Mono

- **Copyright:** © 2017 IBM Corp. with Reserved Font Name "Plex"
- **License:** SIL Open Font License, Version 1.1 (OFL-1.1)
- **Upstream:** https://github.com/IBM/plex
- **npm package:** `@fontsource/ibm-plex-mono@5.2.7`

## Compliance notes

- **Bundling is explicitly permitted** under OFL-1.1 — the fonts remain under OFL
  regardless of the license of the software they're bundled with (RunCoach is
  Apache-2.0; see root `NOTICE`).
- **No active attribution is required** by OFL-1.1 (FAQ 1.1.2); this file is
  documentation best practice, not a compliance obligation.
- **No modification is made** to any of these families in this repository — only
  unmodified, granular per-weight, latin-subset CSS/woff2 files from the official
  `@fontsource/*` distributions are imported (`frontend/src/index.css`). IBM Plex
  Mono's Reserved Font Name ("Plex") is therefore not implicated — that restriction
  only applies to modified/derivative redistributions.
- The OFL license text and copyright headers ship inside each `@fontsource/*`
  package (`node_modules/@fontsource/{barlow,barlow-condensed,ibm-plex-mono}/LICENSE`)
  and are installed alongside the fonts via `npm install`.
