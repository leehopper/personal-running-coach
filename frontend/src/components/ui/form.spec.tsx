import * as React from 'react'
import { render, screen } from '@testing-library/react'
import { useForm } from 'react-hook-form'
import { describe, expect, it } from 'vitest'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from './form'
import { Input } from './input'

interface HarnessValues {
  email: string
}

// Minimal react-hook-form harness exercising the shared FormItem/Control/Message
// stack the auth and logging surfaces compose. `errorMessage` seeds a manual
// field error after mount so the assertion reflects the runtime error path.
const FormMessageHarness = ({ errorMessage }: { errorMessage?: string }) => {
  const form = useForm<HarnessValues>({ defaultValues: { email: '' } })

  React.useEffect(() => {
    if (errorMessage !== undefined) {
      form.setError('email', { type: 'manual', message: errorMessage })
    }
  }, [errorMessage, form])

  return (
    <Form {...form}>
      <FormField
        control={form.control}
        name="email"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Email</FormLabel>
            <FormControl>
              <Input {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    </Form>
  )
}

// A harness that renders static children through FormMessage with no field
// error, exercising the non-error branch where role="alert" must be omitted.
const FormMessageChildrenHarness = ({ children }: { children: React.ReactNode }) => {
  const form = useForm<HarnessValues>({ defaultValues: { email: '' } })

  return (
    <Form {...form}>
      <FormField
        control={form.control}
        name="email"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Email</FormLabel>
            <FormControl>
              <Input {...field} />
            </FormControl>
            <FormMessage>{children}</FormMessage>
          </FormItem>
        )}
      />
    </Form>
  )
}

describe('FormMessage', () => {
  it('announces a validation error assertively via role="alert" (#560)', async () => {
    render(<FormMessageHarness errorMessage="Email is required" />)

    const message = await screen.findByText('Email is required')
    expect(message).toHaveAttribute('role', 'alert')
  })

  it('renders no alert element when the field has no error', () => {
    render(<FormMessageHarness />)

    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('renders static children without an alert role when there is no error', () => {
    render(<FormMessageChildrenHarness>Passwords must match</FormMessageChildrenHarness>)

    const note = screen.getByText('Passwords must match')
    expect(note).not.toHaveAttribute('role')
  })
})
