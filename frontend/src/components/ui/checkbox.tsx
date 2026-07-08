import * as React from 'react'
import { CheckIcon } from 'lucide-react'
import { Checkbox as CheckboxPrimitive } from 'radix-ui'

import { cn } from '@/lib/utils'

function Checkbox({ className, ...props }: React.ComponentProps<typeof CheckboxPrimitive.Root>) {
  return (
    <CheckboxPrimitive.Root
      data-slot="checkbox"
      className={cn(
        // `relative` + the `before` pseudo-element expand the click/tap area
        // to a 44px square around the 16px visual box without changing its
        // size (mirrors radio-group-item's identical technique).
        "peer relative size-4 shrink-0 rounded-xs border border-input outline-none transition-[transform,background-color,border-color,opacity] duration-150 ease-out before:absolute before:inset-[-14px] before:content-[''] motion-reduce:transition-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] disabled:pointer-events-none disabled:opacity-35 data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground aria-invalid:border-destructive aria-invalid:ring-destructive/[0.22]",
        className,
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator
        data-slot="checkbox-indicator"
        className="flex items-center justify-center text-current"
      >
        <CheckIcon className="size-3.5" />
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  )
}

export { Checkbox }
