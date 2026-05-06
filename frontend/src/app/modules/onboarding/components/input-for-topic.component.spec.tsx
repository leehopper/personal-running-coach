import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { OnboardingTopic, SuggestedInputType } from '~/modules/onboarding/models/onboarding.model'
import { InputForTopic } from './input-for-topic.component'
import { InputForTopicMap } from './input-for-topic.helpers'

describe('InputForTopic dispatcher', () => {
  it('exposes a component for every SuggestedInputType', () => {
    const allInputTypes: SuggestedInputType[] = [
      SuggestedInputType.Text,
      SuggestedInputType.SingleSelect,
      SuggestedInputType.MultiSelect,
      SuggestedInputType.Numeric,
      SuggestedInputType.Date,
    ]
    for (const inputType of allInputTypes) {
      expect(InputForTopicMap[inputType]).toBeTypeOf('function')
    }
  })

  it('renders the text input for SuggestedInputType.Text', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Text}
        topic={OnboardingTopic.InjuryHistory}
        onSubmit={vi.fn()}
      />,
    )
    expect(screen.getByTestId('text-turn-input')).toBeInTheDocument()
  })

  it('renders the single-select input for SuggestedInputType.SingleSelect', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.SingleSelect}
        topic={OnboardingTopic.PrimaryGoal}
        onSubmit={vi.fn()}
      />,
    )
    expect(screen.getByTestId('single-select-turn-input')).toBeInTheDocument()
  })

  it('renders the multi-select input for SuggestedInputType.MultiSelect', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.MultiSelect}
        topic={OnboardingTopic.WeeklySchedule}
        onSubmit={vi.fn()}
      />,
    )
    expect(screen.getByTestId('multi-select-turn-input')).toBeInTheDocument()
  })

  it('renders the numeric input for SuggestedInputType.Numeric', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Numeric}
        topic={OnboardingTopic.CurrentFitness}
        onSubmit={vi.fn()}
      />,
    )
    expect(screen.getByTestId('numeric-turn-input')).toBeInTheDocument()
  })

  it('renders the date input for SuggestedInputType.Date', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Date}
        topic={OnboardingTopic.TargetEvent}
        onSubmit={vi.fn()}
      />,
    )
    expect(screen.getByTestId('date-turn-input')).toBeInTheDocument()
  })

  it('forwards onSubmit through the dispatcher to the chosen text input', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Text}
        topic={OnboardingTopic.InjuryHistory}
        onSubmit={onSubmit}
      />,
    )

    await user.type(screen.getByTestId('text-turn-input-field'), 'no recent injuries')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSubmit).toHaveBeenCalledWith({ text: 'no recent injuries' })
  })

  it('forwards isSubmitting to the chosen input (disables its submit button)', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Text}
        topic={OnboardingTopic.InjuryHistory}
        onSubmit={vi.fn()}
        isSubmitting={true}
      />,
    )
    expect(screen.getByRole('button', { name: /sending/i })).toBeDisabled()
  })

  it('forwards options to single-select input', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.SingleSelect}
        topic={OnboardingTopic.PrimaryGoal}
        onSubmit={vi.fn()}
        options={[
          { value: 'alpha', label: 'Alpha label' },
          { value: 'beta', label: 'Beta label' },
        ]}
      />,
    )
    expect(screen.getByLabelText('Alpha label')).toBeInTheDocument()
    expect(screen.getByLabelText('Beta label')).toBeInTheDocument()
  })

  it('forwards options to multi-select input', () => {
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.MultiSelect}
        topic={OnboardingTopic.WeeklySchedule}
        onSubmit={vi.fn()}
        options={[
          { value: 'mon', label: 'Mon' },
          { value: 'wed', label: 'Wed' },
        ]}
      />,
    )
    expect(screen.getByLabelText('Mon')).toBeInTheDocument()
    expect(screen.getByLabelText('Wed')).toBeInTheDocument()
  })

  it('numeric input rejects empty / non-numeric values via Zod', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Numeric}
        topic={OnboardingTopic.CurrentFitness}
        onSubmit={onSubmit}
      />,
    )
    // Default value is 0, which is invalid (must be > 0).
    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()

    await user.type(screen.getByTestId('numeric-turn-input-field'), '40')
    await user.click(screen.getByRole('button', { name: /send/i }))
    expect(onSubmit).toHaveBeenCalledWith({ text: '40' })
  })

  it('date input requires a YYYY-MM-DD string', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.Date}
        topic={OnboardingTopic.TargetEvent}
        onSubmit={onSubmit}
      />,
    )

    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
    // userEvent.type does not feed a full ISO date into `<input type="date">`
    // in jsdom — fireEvent.change is the supported path for this control.
    fireEvent.change(screen.getByTestId('date-turn-input-field'), {
      target: { value: '2026-09-01' },
    })

    await user.click(screen.getByRole('button', { name: /send/i }))
    expect(onSubmit).toHaveBeenCalledWith({ text: '2026-09-01' })
  })

  it('multi-select submits comma-joined picked values', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(
      <InputForTopic
        suggestedInputType={SuggestedInputType.MultiSelect}
        topic={OnboardingTopic.WeeklySchedule}
        onSubmit={onSubmit}
      />,
    )

    await user.click(screen.getByLabelText('Mon'))
    await user.click(screen.getByLabelText('Wed'))
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(onSubmit).toHaveBeenCalledWith({ text: 'monday,wednesday' })
  })
})
