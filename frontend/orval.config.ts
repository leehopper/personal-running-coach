import { defineConfig } from 'orval'

// Orval v8 codegen reads the committed OpenAPI spec emitted by the backend's
// `EmitOpenApi` MSBuild target (DEC-066 / R-071) and produces one Zod schema
// per DTO under `src/api/generated/zod/`. Schemas are consumed via the
// hand-maintained barrel at `src/api/generated/index.ts` which re-exports the
// generator's snake_case kebab-style filenames under project-conventional
// camelCase names (e.g. `RegisterRequestDtoSchema` -> `registerRequestSchema`).
//
// Output is committed and gated by `codegen:check` so schema changes that
// aren't re-emitted before push fail CI loudly. `strict: true` produces
// `z.object({...}).strict()` so unknown wire fields fail validation rather
// than silently passing through.
export default defineConfig({
  zod: {
    input: '../backend/openapi/swagger.json',
    output: {
      target: './src/app/api/generated/zod',
      client: 'zod',
      mode: 'tags-split',
      fileExtension: '.ts',
      override: {
        zod: {
          strict: {
            response: true,
            query: true,
            param: true,
            header: true,
            body: true,
          },
        },
      },
    },
  },
})
