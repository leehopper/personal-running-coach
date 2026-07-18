import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'

import { DateChip } from './date-chip.component'

describe('DateChip', () => {
  it('renders the formatted date-chip label (sentence-case source text, uppercased via CSS) for the given ISO value', () => {
    render(<DateChip value="2026-07-08" onChange={vi.fn()} />)

    const label = screen.getByText('Wed, Jul 8')
    expect(screen.getByTestId('log-date-chip')).toContainElement(label)
    expect(label).toHaveClass('uppercase')
  })

  it('renders the "Select date" placeholder instead of a garbage label when value is cleared', () => {
    render(<DateChip value="" onChange={vi.fn()} />)

    expect(screen.getByTestId('log-date-chip')).toHaveTextContent('Select date')
  })

  it('forwards a ref and injected FormControl props (id, aria-describedby, aria-invalid) onto the native input', () => {
    const ref = { current: null as HTMLInputElement | null }
    render(
      <DateChip
        ref={ref}
        value="2026-07-08"
        onChange={vi.fn()}
        id="occurredOn"
        aria-describedby="err-1"
        aria-invalid="true"
      />,
    )

    const input = screen.getByLabelText('Date')
    expect(input).toHaveAttribute('id', 'occurredOn')
    expect(input).toHaveAttribute('aria-describedby', 'err-1')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(ref.current).toBe(input)
  })

  it('exposes the native date input with accessible name "Date" and the given value', () => {
    render(<DateChip value="2026-07-08" onChange={vi.fn()} />)

    const input = screen.getByLabelText('Date') as HTMLInputElement
    expect(input).toHaveAttribute('type', 'date')
    expect(input.value).toBe('2026-07-08')
  })

  it('fires onChange with the new ISO string when the native input changes', () => {
    const onChange = vi.fn()
    render(<DateChip value="2026-07-08" onChange={onChange} />)

    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2026-07-09' } })

    expect(onChange).toHaveBeenCalledExactlyOnceWith('2026-07-09')
  })

  it('accepts and forwards a back-dated (past) date unchanged', () => {
    const onChange = vi.fn()
    render(<DateChip value="2026-07-08" onChange={onChange} />)

    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2020-01-01' } })

    expect(onChange).toHaveBeenCalledExactlyOnceWith('2020-01-01')
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(<DateChip value="2026-07-08" onChange={vi.fn()} />)
    expectDualThemeParity(result, 'log-date-chip')
  })
})
