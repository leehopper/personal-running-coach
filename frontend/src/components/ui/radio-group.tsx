import * as React from 'react'
import { CircleIcon } from 'lucide-react'
import { RadioGroup as RadioGroupPrimitive } from 'radix-ui'

import { cn } from '@/lib/utils'

function RadioGroup({
  className,
  ...props
}: React.ComponentProps<typeof RadioGroupPrimitive.Root>) {
  return (
    <RadioGroupPrimitive.Root
      data-slot="radio-group"
      className={cn('grid gap-3', className)}
      {...props}
    />
  )
}

function RadioGroupItem({
  className,
  ...props
}: React.ComponentProps<typeof RadioGroupPrimitive.Item>) {
  return (
    <RadioGroupPrimitive.Item
      data-slot="radio-group-item"
      className={cn(
        // `relative` + the `before` pseudo-element expand the click/tap area to
        // a 44px square around the 16px visual dot without changing its size.
        "relative aspect-square size-4 shrink-0 rounded-full border border-input text-primary outline-none transition-[transform,background-color,border-color,opacity] duration-150 ease-out before:absolute before:inset-[-14px] before:content-[''] motion-reduce:transition-none data-[state=checked]:border-primary focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] disabled:pointer-events-none disabled:opacity-35 aria-invalid:border-destructive aria-invalid:ring-destructive/[0.22]",
        className,
      )}
      {...props}
    >
      <RadioGroupPrimitive.Indicator
        data-slot="radio-group-indicator"
        className="relative flex items-center justify-center"
      >
        <CircleIcon className="absolute top-1/2 left-1/2 size-2 -translate-x-1/2 -translate-y-1/2 fill-primary" />
      </RadioGroupPrimitive.Indicator>
    </RadioGroupPrimitive.Item>
  )
}

export { RadioGroup, RadioGroupItem }
