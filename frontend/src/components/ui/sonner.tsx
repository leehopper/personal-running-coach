import { type CSSProperties } from 'react'
import {
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  SquareCheckIcon,
  TriangleAlertIcon,
} from 'lucide-react'
import { Toaster as Sonner, type ToasterProps } from 'sonner'
import { useDocumentTheme } from '@/components/use-document-theme.hooks'

// Sonner ships its own stylesheet the moment the module loads, and several
// of its rules (the toast border, the action-button box, the action-button
// focus ring) are more specific than a single utility class. The trailing
// `!` below forces those particular utilities to win against that shipped
// stylesheet; it is not a general styling habit, just the escape hatch this
// one third-party surface needs.
//
// Contrast across every interactive state: resting/idle renders clay text
// (`text-clay-text`) on the toast surface (bg-transparent over the toast's
// `--normal-bg`, i.e. `--popover`/`--alp-surface`) — measured ~4.70:1 dark /
// ~4.68:1 light, clearing AA. Hovering swaps the fill to `--accent` (raised)
// AND the text to `--accent-foreground`, an already-gated pair (~12.4:1
// both modes). The bug this fixes: pressing WITHOUT hovering first (the
// common touch-tap path — most touch browsers never engage `:hover`) used
// to leave `active:bg-secondary` (the same raised fill) under the untouched
// clay text, measuring only ~4.20:1 in dark — a real AA failure, not a
// hover-only edge case. `active:text-secondary-foreground` pairs the
// pressed fill with its own matching foreground (the same alp-bone
// primitive as `--accent-foreground`, already gated at ~12.4:1 both modes)
// instead of introducing a new token, so pressing reads exactly like
// pressing any other outline-style control (button.tsx's `outline` variant
// has the same bg-secondary press-darken with no clay text to begin with).
const RETRY_ACTION_BUTTON_CLASSNAME =
  'rounded-md! border! border-input! min-h-11 bg-transparent! px-3! font-condensed text-[13px]! font-bold! tracking-[0.12em] uppercase text-clay-text! outline-none transition-[transform,background-color,border-color,opacity]! duration-150! ease-out! motion-reduce:transition-none! hover:bg-accent! hover:text-accent-foreground! active:scale-[0.98] active:bg-secondary! active:text-secondary-foreground! focus-visible:border-ring! focus-visible:ring-[3px]! focus-visible:ring-ring/[0.22]!'

// Toaster mirrors the `.dark` class on `documentElement` rather than relying
// on `next-themes`, which this Vite SPA does not use.
const Toaster = ({ ...props }: ToasterProps) => {
  const theme = useDocumentTheme()

  return (
    <Sonner
      theme={theme}
      className="toaster group"
      icons={{
        success: <SquareCheckIcon className="size-4 text-positive" />,
        info: <InfoIcon className="size-4" />,
        warning: <TriangleAlertIcon className="size-4" />,
        error: <OctagonXIcon className="size-4 text-destructive" />,
        loading: <Loader2Icon className="size-4 animate-spin motion-reduce:animate-none" />,
      }}
      toastOptions={{
        classNames: {
          // Toast copy reads as a short data readout ("RUN LOGGED — 9.2 KM"),
          // not prose, so it takes the mono role like other label/data text.
          title: 'font-mono',
          description: 'font-mono',
          // Danger accent: a left edge in the destructive color, distinct
          // from success's quieter treatment (an accent-colored icon only).
          error: 'border-l-4! border-l-destructive!',
          actionButton: RETRY_ACTION_BUTTON_CLASSNAME,
        },
      }}
      style={
        {
          '--normal-bg': 'var(--popover)',
          '--normal-text': 'var(--popover-foreground)',
          '--normal-border': 'var(--border)',
          '--border-radius': 'var(--radius)',
        } as CSSProperties
      }
      {...props}
    />
  )
}

export { Toaster }
