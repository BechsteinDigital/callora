/**
 * The context keys Communication publishes, as the blocks name them.
 *
 * Kept beside the blocks rather than inlined: a key is a contract with the server half, and
 * a typo in a string literal is a block that silently never updates.
 */

/** A call ringing in, waiting to be answered. */
export const INCOMING_CALL_KEY = 'communication.incoming-call/v1'

/** The conversation in progress. */
export const ACTIVE_CALL_KEY = 'communication.active-call/v1'

/** One call as the context publishes it. */
export interface SurfaceCallView {
  callId: string
  remoteParty: string
  direction: string
  state: string
  /** When the call reached this state — a panel counts up from it. */
  since: string
}
