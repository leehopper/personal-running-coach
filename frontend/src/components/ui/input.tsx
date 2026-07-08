import * as React from 'react'

import { cn } from '@/lib/utils'

function Input({ className, type, ...props }: React.ComponentProps<'input'>) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        'flex h-9 min-h-11 w-full min-w-0 rounded-md border border-input bg-input-fill px-3 py-1 font-body text-base text-foreground transition-[transform,background-color,border-color,opacity] duration-150 ease-out motion-reduce:transition-none outline-none selection:bg-primary selection:text-primary-foreground file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground disabled:pointer-events-none disabled:opacity-35 md:text-sm',
        'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22]',
        'aria-invalid:border-destructive aria-invalid:ring-destructive/[0.22]',
        className,
      )}
      {...props}
    />
  )
}

export { Input }
