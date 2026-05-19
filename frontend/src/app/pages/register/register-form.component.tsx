import type { UseFormReturn } from 'react-hook-form'

import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { RegisterRequest } from '~/api/generated'

export interface RegisterFormProps {
  form: UseFormReturn<RegisterRequest>
  onSubmit: (values: RegisterRequest) => Promise<void> | void
  isLoading: boolean
  formAlert: string | null
}

export const RegisterForm = ({ form, onSubmit, isLoading, formAlert }: RegisterFormProps) => {
  const isSubmitDisabled = !form.formState.isValid || isLoading

  return (
    <>
      <h1 className="mb-4 text-2xl font-semibold">Create your account</h1>

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
                  <Input type="password" autoComplete="new-password" {...field} />
                </FormControl>
                <FormDescription>
                  At least 12 characters. The server also requires an uppercase letter, a lowercase
                  letter, a digit, and a non-alphanumeric character; a weaker password is rejected
                  at submit with a server-side error.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button type="submit" disabled={isSubmitDisabled} className="w-full">
            {isLoading ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
      </Form>
    </>
  )
}
