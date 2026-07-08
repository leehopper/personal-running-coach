import type { ReactElement } from 'react'
import { Outlet } from 'react-router-dom'
import { TabBar, TAB_BAR_CLEARANCE } from '~/modules/app/components/tab-bar/tab-bar.component'

/**
 * Shared shell for every authenticated, onboarded route: a scrollable
 * content region (the routed page via `<Outlet />`) plus the fixed
 * `<TabBar />`. The content region is padded by `TAB_BAR_CLEARANCE` so no
 * page content ever renders behind the bar (spec § AD2). `/login`,
 * `/register`, and `/onboarding` are NOT nested under this layout — they
 * render with no tab bar at all.
 */
export const ShellLayout = (): ReactElement => (
  <div className="min-h-screen bg-background">
    <div style={{ paddingBottom: TAB_BAR_CLEARANCE }}>
      <Outlet />
    </div>
    <TabBar />
  </div>
)
