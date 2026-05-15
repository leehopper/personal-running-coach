import { z } from 'zod'

// Login deliberately does NOT duplicate the registration password rules —
// timing-safe login treats all failure modes identically, so pre-validating
// complexity on the client would leak a side-channel ("your stored password
// cannot meet these rules, so you are definitely unknown"). Accept any
// non-empty string and let the server return 401 uniformly.
//
// The register schema lived here in Slice 0 as a hand-rolled mirror of the
// backend DataAnnotations on `RegisterRequestDto`; T03.1 retired it in
// favour of the generated `registerRequestSchema` exported by
// `~/api/generated` (DEC-066 / R-071). Login isn't a candidate for the
// same migration because the server enforces nothing beyond "present" on
// the wire — the local non-empty refinement keeps the form's "Sign in"
// button disabled until both fields carry a value.
export const loginSchema = z.object({
  email: z.string().trim().min(1, 'Email is required.'),
  password: z.string().min(1, 'Password is required.'),
})

export type LoginFormValues = z.infer<typeof loginSchema>
