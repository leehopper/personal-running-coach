import * as React from 'react'
import { RadioGroup as SegmentedControlPrimitive } from 'radix-ui'

import { cn } from '@/lib/utils'

// Built on Radix's RadioGroup rather than ToggleGroup: a segmented control is
// a mutually-exclusive single-choice picker that must always keep exactly one
// segment selected (COMPLETED/PARTIAL/SKIPPED, KM/MI, DARK/LIGHT/SYSTEM).
// ToggleGroup's `type="single"` mode allows the active item to be toggled off
// by re-clicking it, which would leave the control with no selection —
// RadioGroup's native semantics rule that out and give the right
// role="radiogroup"/role="radio" + arrow-key roving-focus behavior for free.
function SegmentedControl({
  className,
  orientation = 'horizontal',
  ...props
}: React.ComponentProps<typeof SegmentedControlPrimitive.Root>) {
  return (
    <SegmentedControlPrimitive.Root
      data-slot="segmented-control"
      orientation={orientation}
      className={cn('inline-flex w-full items-center gap-0.5 rounded-md bg-muted p-0.5', className)}
      {...props}
    />
  )
}

function SegmentedControlItem({
  className,
  children,
  ...props
}: React.ComponentProps<typeof SegmentedControlPrimitive.Item>) {
  return (
    <SegmentedControlPrimitive.Item
      data-slot="segmented-control-item"
      className={cn(
        'inline-flex h-9 min-h-11 flex-1 items-center justify-center rounded-sm font-condensed text-[13px] font-bold tracking-[0.1em] text-muted-foreground uppercase transition-[transform,background-color,color,opacity] duration-150 ease-out motion-reduce:transition-none outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] disabled:pointer-events-none disabled:opacity-35 active:scale-[0.98] data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground data-[state=unchecked]:active:bg-secondary data-[state=checked]:active:bg-clay-pressed',
        className,
      )}
      {...props}
    >
      {children}
    </SegmentedControlPrimitive.Item>
  )
}

export { SegmentedControl, SegmentedControlItem }
