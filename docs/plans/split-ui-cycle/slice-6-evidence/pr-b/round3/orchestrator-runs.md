# Slice 6 PR-B round 3: the CodeRabbit thread on PR #364 (host, 2026-09-06)

One review thread: the register helper's numeral inherited `font-mono`; the house rule (frontend/CLAUDE.md Typography) puts numbers in the condensed font. Fix F1 wraps the `12` in `font-condensed` and adds `renders the helper numeral in the condensed font` to the register spec (red without the span).

```text
npm run test -> Tests  1066 passed (1066)
eslint exit 0 on the two touched files; npm run build clean (fixer); prettier clean (fixer)
```
