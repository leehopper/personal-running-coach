import * as React from 'react'
import { Switch as SwitchPrimitive } from 'radix-ui'

import { cn } from '@/lib/utils'

// The interactive element (Root) fills a full 44px hit area even though the
// visible track is smaller — the track is a plain decorative span painted via
// `group-data-[state=…]` (Root carries the real `data-state`, the span reads
// it through the `group` utility) so the click/tap target stays generous
// without inflating the visual control.
function Switch({ className, ...props }: React.ComponentProps<typeof SwitchPrimitive.Root>) {
  return (
    <SwitchPrimitive.Root
      data-slot="switch"
      className={cn(
        'group peer inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full outline-none transition-[transform,background-color,opacity] duration-150 ease-out motion-reduce:transition-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] active:scale-[0.98] disabled:pointer-events-none disabled:opacity-35',
        className,
      )}
      {...props}
    >
      <span
        aria-hidden="true"
        data-slot="switch-track"
        className="pointer-events-none relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border border-input bg-input-fill transition-[background-color,border-color] duration-150 ease-out motion-reduce:transition-none group-data-[state=checked]:border-primary group-data-[state=checked]:bg-primary"
      >
        <SwitchPrimitive.Thumb
          data-slot="switch-thumb"
          className="pointer-events-none block size-5 translate-x-0.5 rounded-full bg-foreground shadow-xs transition-transform duration-150 ease-out motion-reduce:transition-none data-[state=checked]:translate-x-5"
        />
      </span>
    </SwitchPrimitive.Root>
  )
}

export { Switch }
