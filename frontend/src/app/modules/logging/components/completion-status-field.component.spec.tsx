import { useEffect } from 'react'
import { useForm, type UseFormReturn } from 'react-hook-form'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { Form } from '@/components/ui/form'
import { renderInBothThemes, testidsIn } from '~/modules/common/test-utils/render-in-both-themes'
import {
  makeDefaultWorkoutLogFormFields,
  type WorkoutLogFormFields,
  type WorkoutLogFormValues,
} from '~/modules/logging/schemas/workout-log-form.schema'
import { CompletionStatusField } from './completion-status-field.component'

type HarnessForm = UseFormReturn<WorkoutLogFormFields, unknown, WorkoutLogFormValues>

interface HarnessProps {
  /** Mutable box the harness stashes its `useForm` instance into, so tests can read `getValues` after an interaction without subscribing via `watch` (which React Compiler flags as unmemoizable). */
  formRef?: { current: HarnessForm | null }
}

// Minimal RHF harness: `CompletionStatusField` requires a real `Control`
// carrying the workout-log form's input/output generics (a hand-built mock
// `control` object would need to fake RHF's internal field-array/subject
// wiring), so this mounts the field against an actual `useForm` instance
// with the form's real defaults — the same "Complete" default and string
// wire values the field carries in production.
const Harness = ({ formRef }: HarnessProps = {}) => {
  const form = useForm<WorkoutLogFormFields, unknown, WorkoutLogFormValues>({
    defaultValues: makeDefaultWorkoutLogFormFields(),
  })
  // Ref writes are only valid outside render (effects, handlers) — `useForm`'s
  // returned object is stable across re-renders, so a mount-time effect is
  // enough to hand it to the test.
  useEffect(() => {
    if (formRef) {
      formRef.current = form
    }
    return () => {
      if (formRef) {
        formRef.current = null
      }
    }
  }, [form, formRef])

  return (
    <Form {...form}>
      <CompletionStatusField control={form.control} name="completionStatus" />
    </Form>
  )
}

describe('CompletionStatusField', () => {
  it('renders three segments — Completed / Partial / Skipped — with the expected testids', () => {
    // jsdom performs no real layout/style resolution, so `textContent` stays
    // the sentence-case source string ("Completed") — the COMPLETED/
    // PARTIAL/SKIPPED rendering is a CSS `uppercase` presentation effect
    // from `SegmentedControlItem`, verified below via the `uppercase` class
    // rather than the (untestable-in-jsdom) rendered glyphs.
    render(<Harness />)

    expect(screen.getByTestId('completion-completed')).toHaveTextContent('Completed')
    expect(screen.getByTestId('completion-partial')).toHaveTextContent('Partial')
    expect(screen.getByTestId('completion-skipped')).toHaveTextContent('Skipped')
    for (const testId of ['completion-completed', 'completion-partial', 'completion-skipped']) {
      expect(screen.getByTestId(testId)).toHaveClass('uppercase')
    }
  })

  it('keeps the "Completion" FormLabel', () => {
    render(<Harness />)

    expect(screen.getByText('Completion')).toBeInTheDocument()
  })

  it("defaults to Completed — form value '0', completed segment radix-checked, the others unchecked", () => {
    const formRef: { current: HarnessForm | null } = { current: null }
    render(<Harness formRef={formRef} />)

    expect(formRef.current?.getValues('completionStatus')).toBe('0')
    expect(screen.getByTestId('completion-completed')).toHaveAttribute('data-state', 'checked')
    expect(screen.getByTestId('completion-partial')).toHaveAttribute('data-state', 'unchecked')
    expect(screen.getByTestId('completion-skipped')).toHaveAttribute('data-state', 'unchecked')
  })

  it("selecting PARTIAL calls onValueChange with the wire string '1' — the same string the radios produced", async () => {
    const user = userEvent.setup()
    const formRef: { current: HarnessForm | null } = { current: null }
    render(<Harness formRef={formRef} />)

    await user.click(screen.getByTestId('completion-partial'))

    expect(formRef.current?.getValues('completionStatus')).toBe('1')
    expect(screen.getByTestId('completion-partial')).toHaveAttribute('data-state', 'checked')
    expect(screen.getByTestId('completion-completed')).toHaveAttribute('data-state', 'unchecked')
  })

  it("selecting SKIPPED calls onValueChange with the wire string '2' — the same string the radios produced", async () => {
    const user = userEvent.setup()
    const formRef: { current: HarnessForm | null } = { current: null }
    render(<Harness formRef={formRef} />)

    await user.click(screen.getByTestId('completion-skipped'))

    expect(formRef.current?.getValues('completionStatus')).toBe('2')
    expect(screen.getByTestId('completion-skipped')).toHaveAttribute('data-state', 'checked')
    expect(screen.getByTestId('completion-completed')).toHaveAttribute('data-state', 'unchecked')
  })

  it('renders identically in both themes with zero raw colour literals', () => {
    const { dark, light } = renderInBothThemes(<Harness />)
    for (const result of [dark, light]) {
      expect(result.getByTestId('completion-completed')).toBeInTheDocument()
      expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
    }
    expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
  })
})
