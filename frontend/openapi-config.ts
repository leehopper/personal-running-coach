import type { ConfigFile } from '@rtk-query/codegen-openapi'

// RTK Query codegen reads the committed OpenAPI spec emitted by the backend's
// `EmitOpenApi` MSBuild target (DEC-066 / R-071) and produces a typed slice
// derived from the swagger paths/tags. Output is written to
// `src/api/generated/rtk/api.ts`; downstream consumers import named hooks via
// the barrel at `src/api/generated/index.ts` once the corresponding hand-rolled
// slice retires (one feature at a time).
//
// Generated output is committed and gated by the `codegen:check` drift gate so
// schema changes that aren't re-emitted before push fail CI loudly, per the
// silent-drift class of bug R-071 motivates this pipeline against.
const config: ConfigFile = {
  schemaFile: '../backend/openapi/swagger.json',

  // The base API slice the generator hooks into. `injectEndpoints` is wired
  // up at runtime in `src/app/api/api-slice.ts`; the generator emits one
  // `injectEndpoints` call wrapping all endpoints derived from the spec.
  apiFile: './src/app/api/api-slice.ts',
  apiImport: 'apiSlice',
  outputFile: './src/app/api/generated/rtk/api.ts',
  exportName: 'generatedApi',

  // Generate React hooks so eventual call-site migration picks up the same
  // `useXxxQuery` / `useXxxMutation` shapes the hand-rolled slices already use.
  hooks: true,
}

export default config
