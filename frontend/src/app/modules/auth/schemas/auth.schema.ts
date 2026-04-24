import { z } from 'zod'

// Email rules mirror `RegisterRequestDto.Email`:
//   [Required, EmailAddress, MaxLength(254)]
// Password rules mirror the backend's ASP.NET Identity Core policy:
//   RequiredLength = 12, RequireUppercase, RequireLowercase, RequireDigit,
//   RequireNonAlphanumeric. Max 128 chars from `RegisterRequestDto.Password`.
// See backend/src/RunCoach.Api/Program.cs lines 108–127 for the canonical
// policy. A contract test in `shared-contracts/` (captured follow-up) will
// assert these two schemas accept/reject the same inputs as the backend
// DataAnnotations — deferred to the shared-contracts folder.

const emailSchema = z
  .email('Email must be a valid address.')
  .trim()
  .min(1, 'Email is required.')
  .max(254, 'Email must be at most 254 characters.')

export const registerSchema = z.object({
  email: emailSchema,
  password: z
    .string()
    .min(12, 'Password must be at least 12 characters.')
    .max(128, 'Password must be at most 128 characters.')
    .refine((value) => /[A-Z]/.test(value), {
      message: 'Password must contain an uppercase letter.',
    })
    .refine((value) => /[a-z]/.test(value), {
      message: 'Password must contain a lowercase letter.',
    })
    .refine((value) => /\d/.test(value), {
      message: 'Password must contain a digit.',
    })
    .refine((value) => /[^A-Za-z0-9]/.test(value), {
      message: 'Password must contain a non-alphanumeric character.',
    }),
})

// Login deliberately does NOT duplicate the registration password rules —
// timing-safe login treats all failure modes identically, so pre-validating
// complexity on the client would leak a side-channel ("your stored password
// cannot meet these rules, so you are definitely unknown"). Accept any
// non-empty string and let the server return 401 uniformly.
export const loginSchema = z.object({
  email: z.string().trim().min(1, 'Email is required.'),
  password: z.string().min(1, 'Password is required.'),
})

export type RegisterFormValues = z.infer<typeof registerSchema>
export type LoginFormValues = z.infer<typeof loginSchema>
