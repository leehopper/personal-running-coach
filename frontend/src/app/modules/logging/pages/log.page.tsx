import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'

import { useCreateWorkoutLogMutation } from '~/api/workout-log.api'
import { LogForm } from '~/modules/logging/components/log-form.component'
import {
  makeDefaultWorkoutLogFormFields,
  toCreateWorkoutLogRequest,
  workoutLogFormSchema,
  type WorkoutLogFormFields,
  type WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'

/**
 * The `/log` route — a mobile-first workout-logging form. Owns the form state,
 * the create mutation, and post-submit navigation. Create is pessimistic
 * (DEC-075): await success → success toast → navigate home; the mutation
 * invalidates the `WorkoutLog` tag so history (PR7) refetches. `OccurredOn`
 * defaults to today; the prescription snapshot is resolved server-side, so the
 * form never sends prescribed values (DEC-076).
 */
const LogPage = () => {
  const navigate = useNavigate()
  const [createWorkoutLog, { isLoading }] = useCreateWorkoutLogMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)
  // One key per mounted form so a retry after a transient failure reuses it and
  // the server dedupes rather than double-logging (DEC-077).
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [])

  const form = useForm<WorkoutLogFormFields, unknown, WorkoutLogFormValues>({
    resolver: zodResolver(workoutLogFormSchema),
    mode: 'onChange',
    shouldUnregister: false,
    defaultValues: makeDefaultWorkoutLogFormFields(),
  })

  const onSubmit = async (values: WorkoutLogFormValues): Promise<void> => {
    setFormAlert(null)
    try {
      await createWorkoutLog(toCreateWorkoutLogRequest(values, idempotencyKey)).unwrap()
      toast.success('Workout logged')
      navigate('/', { replace: true })
    } catch {
      setFormAlert('We could not save your workout. Please try again in a moment.')
    }
  }

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-md flex-col gap-6 bg-background px-4 py-8"
      data-testid="log-page"
    >
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Log a workout</h1>
        <p className="text-sm text-muted-foreground">Record what you actually ran.</p>
      </header>
      <LogForm form={form} onSubmit={onSubmit} isLoading={isLoading} formAlert={formAlert} />
    </main>
  )
}

export default LogPage
export { LogPage }
