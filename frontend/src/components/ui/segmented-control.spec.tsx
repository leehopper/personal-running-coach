import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SegmentedControl, SegmentedControlItem } from './segmented-control'

describe('SegmentedControl', () => {
  it('renders each option as a radio with exactly one selected', () => {
    render(
      <SegmentedControl aria-label="Distance units" defaultValue="km">
        <SegmentedControlItem value="km">KM</SegmentedControlItem>
        <SegmentedControlItem value="mi">MI</SegmentedControlItem>
      </SegmentedControl>,
    )

    expect(screen.getByRole('radio', { name: 'KM' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('radio', { name: 'MI' })).toHaveAttribute('aria-checked', 'false')
  })

  it('selects an option on click and reports the new value', async () => {
    const user = userEvent.setup()
    const onValueChange = vi.fn()
    render(
      <SegmentedControl aria-label="Distance units" defaultValue="km" onValueChange={onValueChange}>
        <SegmentedControlItem value="km">KM</SegmentedControlItem>
        <SegmentedControlItem value="mi">MI</SegmentedControlItem>
      </SegmentedControl>,
    )

    await user.click(screen.getByRole('radio', { name: 'MI' }))

    expect(onValueChange).toHaveBeenCalledWith('mi')
    expect(screen.getByRole('radio', { name: 'MI' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('radio', { name: 'KM' })).toHaveAttribute('aria-checked', 'false')
  })

  // Arrow-key roving selection (ArrowLeft/ArrowRight moving focus between
  // segments and auto-selecting the newly focused one) is Radix RadioGroup's
  // native behavior, built on its RovingFocusGroup primitive — this thin
  // wrapper does not implement any of it itself. It is intentionally not
  // unit-tested here: RovingFocusGroupItem defers the actual `.focus()` call
  // via `setTimeout(() => focusFirst(...))`, and only auto-selects the newly
  // focused radio (via a synthetic `.click()`) if a document-level "an arrow
  // key is currently held down" ref is still `true` when that deferred
  // callback runs (cleared on the corresponding `keyup`). Confirmed directly:
  // `userEvent.keyboard('{ArrowRight}')` dispatches the synthetic
  // keydown/keyup pair back-to-back with no elapsed time between them, so in
  // jsdom the `keyup` always clears that ref before the deferred `setTimeout`
  // fires. The result is that focus genuinely does move to the next segment
  // (verified with `document.activeElement`), but the auto-select `click()`
  // never happens and `onValueChange` is never called — a jsdom/user-event
  // timing gap in Radix's own implementation detail, not a defect in
  // SegmentedControl, and it's already covered by Radix's own test suite
  // upstream. What this component IS responsible for — routing every
  // selection through `onValueChange` while keeping exactly one segment
  // checked — is covered below via direct clicks, which don't depend on
  // this timing-sensitive path.
  it('keeps exactly one segment active as selection moves between all three options', async () => {
    const user = userEvent.setup()
    const onValueChange = vi.fn()
    render(
      <SegmentedControl
        aria-label="Completion"
        defaultValue="completed"
        onValueChange={onValueChange}
      >
        <SegmentedControlItem value="completed">COMPLETED</SegmentedControlItem>
        <SegmentedControlItem value="partial">PARTIAL</SegmentedControlItem>
        <SegmentedControlItem value="skipped">SKIPPED</SegmentedControlItem>
      </SegmentedControl>,
    )

    const segments = [
      screen.getByRole('radio', { name: 'COMPLETED' }),
      screen.getByRole('radio', { name: 'PARTIAL' }),
      screen.getByRole('radio', { name: 'SKIPPED' }),
    ]

    const checkedSegments = (): HTMLElement[] =>
      segments.filter((segment) => segment.getAttribute('aria-checked') === 'true')

    await user.click(segments[1])
    expect(onValueChange).toHaveBeenLastCalledWith('partial')
    expect(checkedSegments()).toEqual([segments[1]])

    await user.click(segments[2])
    expect(onValueChange).toHaveBeenLastCalledWith('skipped')
    expect(checkedSegments()).toEqual([segments[2]])

    await user.click(segments[0])
    expect(onValueChange).toHaveBeenLastCalledWith('completed')
    expect(checkedSegments()).toEqual([segments[0]])
  })
})
