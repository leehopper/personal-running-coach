## Fixes

- F1 - MECHANIZED: Wrapped `12` in `font-condensed` while preserving mono helper text. Added regression assertion. See [register-form.component.tsx:40](frontend/src/app/pages/register/register-form.component.tsx:40) and [register.page.spec.tsx:169](frontend/src/app/pages/register/register.page.spec.tsx:169). Removing the span makes the numeral assertion fail.

## Gates

- PASS `npm run build` - last output: `- Adjust chunk size limit for this warning via build.chunkSizeWarningLimit.`
- PASS `npx vitest run src/app/pages/register/register.page.spec.tsx` - `Tests 20 passed (20)`.
- PASS `npx eslint src/app/pages/register/register-form.component.tsx src/app/pages/register/register.page.spec.tsx` - exit 0, no output.
- PASS `npx prettier --check ...` - `All matched files use Prettier code style!`
- PASS `git diff --check` - exit 0, no output.

## Deviations

No fixes were blocked. No backend files changed, so backend gates were not applicable. Pre-existing untracked evidence directories were left untouched.

## Open questions

None.

FIX ROUND COMPLETE

Codex session ID: 01a0771f-46a5-7f73-a8ed-ea94553e6521
Resume in Codex: codex resume 01a0771f-46a5-7f73-a8ed-ea94553e6521
