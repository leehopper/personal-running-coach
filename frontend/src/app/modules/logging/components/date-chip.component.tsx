import { forwardRef, type ComponentPropsWithoutRef } from 'react'
import { CalendarIcon, ChevronDownIcon } from 'lucide-react'

import { cn } from '@/lib/utils'
import { formatDateChipLabel } from '~/modules/logging/log-derivations.helpers'

/**
 * Props for {@link DateChip}. Extends the native `<input>` props (minus the
 * ones this component owns) so that RHF's `FormControl` — a Radix `Slot` —
 * can merge `id`, `aria-describedby`, and `aria-invalid` onto the real
 * control; see the component doc for the forwarding contract.
 */
export interface DateChipProps extends Omit<
  ComponentPropsWithoutRef<'input'>,
  'value' | 'onChange' | 'type' | 'className'
> {
  /** ISO `YYYY-MM-DD` date-only string — never a full timestamp. */
  value: string
  /** Called with the new ISO `YYYY-MM-DD` string on every native picker change. */
  onChange: (iso: string) => void
  className?: string
}

/**
 * Tappable date affordance for the /log form — a chip reading
 * `CalendarIcon` + `formatDateChipLabel(value)` + `ChevronDownIcon`. The
 * label span carries `formatDateChipLabel`'s sentence-case source string
 * (e.g. `"Wed, Jul 8"`) plus a CSS `uppercase` class, so it still renders
 * visually uppercase (e.g. "WED, JUL 8") even though the underlying string
 * stays sentence case. This is a purely presentational wrapper around a
 * native `<input type="date">` — no custom calendar widget, no
 * date-parsing logic of its own. The native input is stretched to fully
 * cover the chip (`absolute inset-0`, `opacity-0`) so a tap anywhere on the
 * chip opens the platform date picker directly, while the chip's own
 * text/icons render underneath as the visible affordance.
 *
 * A11y contract (do not break): the native input keeps `aria-label="Date"`
 * as its ONLY accessible name — existing form/E2E coverage locates it via
 * `getByLabelText('Date')`. Keyboard users tab to the (invisible but very
 * real) input; `has-[:focus-visible]` on the chip surfaces the shared focus
 * ring around the visible chip even though the input itself renders
 * transparent.
 *
 * `forwardRef`'d so RHF's `FormControl` (a Radix `Slot`) can attach its ref
 * and spread its injected `id`/`aria-describedby`/`aria-invalid` onto the
 * actual native `<input>`. `...rest` is spread before the explicit
 * `aria-label`/`value`/`onChange` below so those three can never be
 * clobbered by an injected prop.
 */
export const DateChip = forwardRef<HTMLInputElement, DateChipProps>(
  ({ value, onChange, className, ...rest }, ref) => (
    <div
      data-testid="log-date-chip"
      className={cn(
        'relative inline-flex min-h-11 items-center gap-1.5 rounded-sm border border-border bg-input-fill px-3 py-2 transition-colors duration-200 ease-out has-[:focus-visible]:border-ring has-[:focus-visible]:ring-[3px] has-[:focus-visible]:ring-ring/[0.22] motion-reduce:transition-none',
        className,
      )}
    >
      <CalendarIcon aria-hidden="true" className="size-4 shrink-0 text-muted-foreground" />
      <span className="t-data-value uppercase text-foreground">{formatDateChipLabel(value)}</span>
      <ChevronDownIcon aria-hidden="true" className="size-4 shrink-0 text-muted-foreground" />
      <input
        type="date"
        {...rest}
        ref={ref}
        aria-label="Date"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="absolute inset-0 z-10 h-full w-full cursor-pointer opacity-0"
      />
    </div>
  ),
)
DateChip.displayName = 'DateChip'
