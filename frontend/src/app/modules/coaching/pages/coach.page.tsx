import type { ReactElement } from 'react'
import { TAB_BAR_CLEARANCE } from '~/modules/app/components/tab-bar/tab-bar.component'
import { CoachChat } from '~/modules/coaching/components/coach-chat.component'

/**
 * The `/coach` route (spec § AD3). A mechanical relocation of the existing
 * `<CoachChat />` — previously mounted mid-page on home — into its own
 * full-height screen so the transcript scrolls and the composer pins
 * directly above the `TabBar`. No turn-kind restyling or header chrome
 * is added here; `CoachChat`'s own behavior
 * contract (streaming, retry, confirm, Edit→`/log`) is untouched.
 */
const CoachPage = (): ReactElement => (
  <div
    className="flex flex-col px-4 py-4"
    style={{ height: `calc(100dvh - ${TAB_BAR_CLEARANCE})` }}
    data-testid="coach-page"
  >
    <CoachChat />
  </div>
)

export default CoachPage
export { CoachPage }
