import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { Slot } from 'radix-ui'

import { cn } from '@/lib/utils'

const buttonVariants = cva(
  "relative inline-flex shrink-0 items-center justify-center gap-2 rounded-md font-condensed text-[15px] leading-[1.2] font-bold tracking-[0.12em] uppercase whitespace-nowrap transition-[transform,background-color,border-color,opacity] duration-150 ease-out before:absolute before:content-[''] motion-reduce:transition-none outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] disabled:pointer-events-none disabled:opacity-35 aria-invalid:border-destructive aria-invalid:ring-destructive/[0.22] [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default:
          'bg-primary text-primary-foreground hover:bg-primary/90 active:bg-clay-pressed active:scale-[0.98]',
        destructive:
          'bg-destructive text-destructive-foreground hover:bg-destructive/90 active:bg-destructive/90 active:scale-[0.98]',
        outline:
          'border bg-background shadow-xs hover:bg-accent hover:text-accent-foreground active:bg-secondary active:scale-[0.98] dark:border-input dark:bg-input/30 dark:hover:bg-input/50',
        secondary:
          'bg-secondary text-secondary-foreground hover:bg-secondary/80 active:bg-secondary active:scale-[0.98]',
        ghost:
          'hover:bg-accent hover:text-accent-foreground active:bg-secondary active:scale-[0.98] dark:hover:bg-accent/50',
        link: 'text-primary underline-offset-4 hover:underline before:content-none',
      },
      size: {
        default: 'h-9 min-h-11 px-4 py-2 has-[>svg]:px-3',
        xs: "h-6 gap-1 rounded-md px-2 text-[13px] has-[>svg]:px-1.5 before:inset-[-10px] [&_svg:not([class*='size-'])]:size-3",
        sm: 'h-8 gap-1.5 rounded-md px-3 has-[>svg]:px-2.5 before:inset-[-6px]',
        lg: 'h-10 min-h-11 rounded-md px-6 has-[>svg]:px-4',
        icon: 'size-9 before:inset-[-4px]',
        'icon-xs': "size-6 rounded-md before:inset-[-10px] [&_svg:not([class*='size-'])]:size-3",
        'icon-sm': 'size-8 before:inset-[-6px]',
        'icon-lg': 'size-10 before:inset-[-2px]',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
)

function Button({
  className,
  variant = 'default',
  size = 'default',
  asChild = false,
  ...props
}: React.ComponentProps<'button'> &
  VariantProps<typeof buttonVariants> & {
    asChild?: boolean
  }) {
  const Comp = asChild ? Slot.Root : 'button'

  return (
    <Comp
      data-slot="button"
      data-variant={variant}
      data-size={size}
      className={cn(buttonVariants({ variant, size, className }))}
      {...(asChild ? props : { type: 'button' as const, ...props })}
    />
  )
}

export { Button, buttonVariants }
