import type { ComponentType, ReactElement } from 'react'
import { Clock, Home, MessageCircle, Plus, SlidersHorizontal } from 'lucide-react'
import { NavLink, type NavLinkRenderProps } from 'react-router-dom'
import { cn } from '@/lib/utils'

// Reserved vertical space the fixed bar occupies at the bottom of the
// viewport, including the raised center action and the safe-area inset.
// `ShellLayout` pads its content region by this amount and `CoachPage`
// sizes its full-height flex column against it — single source of truth so
// the two never drift out of sync.
export const TAB_BAR_CLEARANCE = 'calc(84px + env(safe-area-inset-bottom))'

interface TabBarLinkItem {
  to: string
  label: string
  icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>
  testId: string
}

const LEADING_ITEMS: readonly TabBarLinkItem[] = [
  { to: '/', label: 'Today', icon: Home, testId: 'tab-today' },
  { to: '/coach', label: 'Coach', icon: MessageCircle, testId: 'tab-coach' },
]

const TRAILING_ITEMS: readonly TabBarLinkItem[] = [
  { to: '/history', label: 'Log book', icon: Clock, testId: 'tab-history' },
  { to: '/settings', label: 'Settings', icon: SlidersHorizontal, testId: 'tab-settings' },
]

const tabLinkClassName = ({ isActive }: NavLinkRenderProps): string =>
  cn(
    'flex min-h-11 flex-col items-center justify-center gap-1 rounded-md transition-colors duration-200 ease-out motion-reduce:transition-none',
    'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] focus-visible:outline-none',
    isActive ? 'text-clay-text' : 'text-muted-foreground',
  )

const TabLink = ({ to, label, icon: Icon, testId }: TabBarLinkItem): ReactElement => (
  <NavLink to={to} end={to === '/'} data-testid={testId} className={tabLinkClassName}>
    <Icon aria-hidden className="size-[21px]" />
    <span className="font-condensed text-[10px] font-semibold tracking-[0.12em] uppercase">
      {label}
    </span>
  </NavLink>
)

/**
 * Fixed bottom primary navigation — the app's single navigation system
 * (SPLIT/Alpine Slice 1). Five columns: TODAY, COACH, a raised center LOG
 * action, LOG BOOK, and SETTINGS. Uses `NavLink` so `aria-current="page"`
 * tracks the active route automatically; the center action always reads as
 * the clay accent regardless of route (spec § Design contract).
 */
export const TabBar = (): ReactElement => (
  <nav
    aria-label="Primary"
    data-testid="tab-bar"
    className="fixed inset-x-0 bottom-0 grid grid-cols-5 items-center border-t border-border bg-card pt-2 pb-[calc(0.75rem+env(safe-area-inset-bottom))]"
  >
    <TabLink {...LEADING_ITEMS[0]} />
    <TabLink {...LEADING_ITEMS[1]} />
    <div className="flex justify-center">
      <NavLink
        to="/log"
        aria-label="Log a workout"
        data-testid="tab-log"
        className="-mt-6.5 flex size-[54px] min-h-11 min-w-11 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg transition-transform duration-200 ease-out focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] focus-visible:outline-none active:scale-[0.98] motion-reduce:transition-none"
      >
        <Plus aria-hidden className="size-6" />
      </NavLink>
    </div>
    <TabLink {...TRAILING_ITEMS[0]} />
    <TabLink {...TRAILING_ITEMS[1]} />
  </nav>
)
