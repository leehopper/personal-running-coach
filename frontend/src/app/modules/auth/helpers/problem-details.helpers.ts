// RFC 7807 Problem Details / Validation Problem Details extractor.
//
// The backend shape is authoritative (DEC-052):
//   ProblemDetails           — { type, title, status, detail?, traceId? }
//   ValidationProblemDetails — ProblemDetails + { errors: { [Field]: string[] } }
//
// `errors` keys are PascalCase DTO property names (`Email`, `Password`); the
// SPA normalizes them back to camelCase form-field names.

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
}

export interface ValidationProblemDetails extends ProblemDetails {
  errors?: Record<string, string[]>
}

export interface ParsedProblem {
  title: string | null
  fieldErrors: Record<string, string[]>
  status: number | null
}

// Safe narrowing against the RTK Query union shape — FetchBaseQueryError is
// a discriminated union covering fetch failure, HTTP error, parsing error,
// and timeout. Only the HTTP-error arm carries a parsed JSON body.
const isHttpError = (error: unknown): error is { status: number; data: unknown } => {
  if (error === null || typeof error !== 'object') return false
  const candidate = error as { status?: unknown; data?: unknown }
  return typeof candidate.status === 'number' && 'data' in candidate
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value)

const isStringArray = (value: unknown): value is string[] =>
  Array.isArray(value) && value.every((item) => typeof item === 'string')

// Accepts `unknown` because callers hand us anything RTK Query or a thrown
// Error can produce; `isHttpError` below narrows to the
// `FetchBaseQueryError` HTTP-error arm at runtime.
export const parseProblem = (error: unknown): ParsedProblem => {
  const empty: ParsedProblem = {
    title: null,
    fieldErrors: {},
    status: null,
  }

  if (!isHttpError(error)) return empty

  const status = error.status
  if (!isRecord(error.data)) return { ...empty, status }

  const body = error.data as ValidationProblemDetails
  const title = typeof body.title === 'string' ? body.title : null

  const fieldErrors: Record<string, string[]> = {}
  if (isRecord(body.errors)) {
    for (const [rawKey, rawValue] of Object.entries(body.errors)) {
      if (!isStringArray(rawValue) || rawValue.length === 0) continue
      const normalized =
        rawKey.length > 0 ? rawKey.charAt(0).toLowerCase() + rawKey.slice(1) : rawKey
      fieldErrors[normalized] = rawValue
    }
  }

  return { title, fieldErrors, status }
}
