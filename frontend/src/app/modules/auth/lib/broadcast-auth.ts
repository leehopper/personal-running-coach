const CHANNEL_NAME = 'auth'
const LOGOUT_MESSAGE = 'logout'

// Cross-tab logout via the native `BroadcastChannel` API — no npm dep
// (spec §Unit 3 line 118). Both functions no-op in environments without
// `BroadcastChannel` (e.g. the jsdom test env, older browsers) so
// callers can subscribe/post unconditionally.
export const postLogoutBroadcast = (): void => {
  if (typeof BroadcastChannel === 'undefined') return
  const channel = new BroadcastChannel(CHANNEL_NAME)
  channel.postMessage(LOGOUT_MESSAGE)
  channel.close()
}

export const subscribeLogoutBroadcast = (handler: () => void): (() => void) => {
  if (typeof BroadcastChannel === 'undefined') return () => undefined
  const channel = new BroadcastChannel(CHANNEL_NAME)
  channel.onmessage = (event) => {
    if (event.data === LOGOUT_MESSAGE) handler()
  }
  return () => channel.close()
}
