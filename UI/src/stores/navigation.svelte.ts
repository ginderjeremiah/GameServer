/* Cross-screen navigation intent. The active in-game screen is local state in
   `routes/game/+page.svelte`; this module-level store lets any screen request a switch to another
   screen, optionally handing it a one-shot `payload` the target consumes on mount (e.g. the
   Statistics screen deep-linking an enemy into the Codex dossier). The `+page.svelte` shell reacts
   to `requestedScreen` and routes through its existing `handleNavigate`. */

let requestedScreen = $state<string | null>(null);
/** A one-shot handoff for the target screen — deliberately NOT reactive (read once on mount). */
let pendingPayload: unknown = undefined;
/** Whether a yet-unconsumed payload is queued. Lets the shell force a remount when the target is
 *  already the active screen (it otherwise wouldn't remount, so its `consumePayload` never runs). */
let payloadPending = false;

/** Whether a screen request targeting the already-active screen must force a remount so the target
 *  re-runs its one-shot `consumePayload` — only when a payload is actually queued for it, so a
 *  same-screen request without a payload leaves the active screen untouched. */
export function requiresRemount(requestedScreen: string, activeScreen: string, hasPendingPayload: boolean): boolean {
	return requestedScreen === activeScreen && hasPendingPayload;
}

export const navigation = {
	/** The screen a component has asked the shell to switch to, or `null` when there's no request. */
	get requestedScreen() {
		return requestedScreen;
	},

	/** Whether a payload is queued and not yet consumed (drives the same-screen remount in the shell). */
	get hasPendingPayload() {
		return payloadPending;
	},

	/** Request a switch to a screen, optionally carrying a one-shot payload for the target screen. */
	requestScreen(key: string, payload?: unknown) {
		requestedScreen = key;
		pendingPayload = payload;
		payloadPending = payload !== undefined;
	},

	/** Read (and clear) the payload handed to the target screen. A one-shot, non-reactive handoff,
	 *  so a later manual navigation to the same screen opens at its defaults. */
	consumePayload<T>(): T | undefined {
		const payload = pendingPayload as T | undefined;
		pendingPayload = undefined;
		payloadPending = false;
		return payload;
	},

	/** Clear the pending screen request once the shell has switched (leaves any payload for the
	 *  target screen to consume on mount). */
	clear() {
		requestedScreen = null;
	},

	/** Clears every piece of navigation intent — the requested screen and any unconsumed payload.
	 *  Used on game teardown so a request queued just before disconnect (e.g. a toast's "View" action)
	 *  doesn't get silently consumed by the next session's game shell on mount. */
	reset() {
		requestedScreen = null;
		pendingPayload = undefined;
		payloadPending = false;
	}
};
