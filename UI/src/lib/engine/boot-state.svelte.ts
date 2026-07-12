/* Cross-route boot signal (#1898). The root layout's boot gate (`+layout.svelte`) decides where a
   fresh page load lands — but Svelte mounts children before the parent's `onMount`, so a route
   mounted directly (e.g. a refresh/tab-restore landing on `/game`) runs its own `onMount` at least
   once *before* the boot gate has even started resuming the session. A route whose mount work isn't
   safe to run pre-boot (`/game`'s welcome-back gate issues a non-idempotent `GetOfflineProgress`)
   needs to know whether the boot decision has already resolved at least once this session.

   Module-level rather than component state: the boot gate's resolution needs to be visible to a
   route mounted independently of the layout, and it only ever needs to go false → true once per
   session (a later route mount after the first boot always finds it already true). */

let booted = $state(false);

export const bootState = {
	get booted() {
		return booted;
	},
	/** Marks the boot gate's decision as resolved. Idempotent; called once by the root layout's boot
	 *  `onMount` after `resumeSession` (or the no-tokens short-circuit) completes. */
	markBooted() {
		booted = true;
	}
};
