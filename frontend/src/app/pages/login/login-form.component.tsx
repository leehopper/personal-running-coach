import type { UseFormReturn } from 'react-hook-form'

import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { LoginFormValues } from '~/modules/auth/schemas/auth.schema'

export interface LoginFormProps {
  form: UseFormReturn<LoginFormValues>
  onSubmit: (values: LoginFormValues) => Promise<void> | void
  isLoading: boolean
  formAlert: string | null
}

export const LoginForm = ({ form, onSubmit, isLoading, formAlert }: LoginFormProps) => {
  const isSubmitDisabled = !form.formState.isValid || isLoading

  return (
    <>
      <h1 className="mb-4 text-2xl font-semibold">Sign in to RunCoach</h1>

      {formAlert !== null && (
        <div
          role="alert"
          data-testid="form-alert"
          className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {formAlert}
        </div>
      )}

      <Form {...form}>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Email</FormLabel>
                <FormControl>
                  <Input type="email" autoComplete="email" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Password</FormLabel>
                <FormControl>
                  <Input type="password" autoComplete="current-password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button type="submit" disabled={isSubmitDisabled} className="w-full">
            {isLoading ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </Form>
    </>
  )
}
