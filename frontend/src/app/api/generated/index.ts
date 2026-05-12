/**
 * Hand-maintained barrel for the codegen pipeline (DEC-066 / R-071).
 *
 * Re-exports the generated artifacts under project-conventional camelCase
 * names so consumers reference `registerRequestSchema` rather than the
 * generator's `PostApiV1AuthRegisterBody`. Renames of the per-feature
 * generated files (e.g. Orval's `auth/auth.ts` → `auth/v2.ts`) are absorbed
 * here so the consuming code doesn't ripple.
 *
 * This file is NOT generated — it is the single seam between the
 * generated output (committed and gated by `codegen:check`) and the rest of
 * the app. Each migrated schema or hook gets one line here per direction
 * (schema + inferred type).
 */
import type { z } from 'zod'

import { PostApiV1AuthRegisterBody } from './zod/auth/auth'

// Auth schemas — migrated piecewise. RegisterRequest is the first; T03.2
// extends to OnboardingProgressDto + SuggestedInputType.
export const registerRequestSchema = PostApiV1AuthRegisterBody
export type RegisterRequest = z.infer<typeof registerRequestSchema>
