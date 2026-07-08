import * as React from 'react'

import { cn } from '@/lib/utils'

function Textarea({ className, ...props }: React.ComponentProps<'textarea'>) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        'min-h-11 w-full rounded-md border border-input bg-input-fill px-3 py-2 font-body text-foreground text-sm transition-[transform,background-color,border-color,opacity] duration-150 ease-out motion-reduce:transition-none outline-none placeholder:text-muted-foreground disabled:pointer-events-none disabled:opacity-35',
        'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22]',
        'aria-invalid:border-destructive aria-invalid:ring-destructive/[0.22]',
        className,
      )}
      {...props}
    />
  )
}

export { Textarea }
