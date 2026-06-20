/* Cross-screen navigation intent. The active in-game screen is local state in
   `routes/game/+page.svelte`; this module-level store lets any screen request a switch to another
   screen, optionally handing it a one-shot `payload` the target consumes on mount (e.g. the
   Statistics screen deep-linking an enemy into the Codex dossier). The `+page.svelte` shell reacts
   to `requestedScreen` and routes through its existing `handleNavigate`. */

let requestedScreen = $state<string | null>(null);
/** A one-shot handoff for the target screen — deliberately NOT reactive (read once on mount). */
let pendingPayload: unknown = undefined;

export const navigation = {
	/** The screen a component has asked the shell to switch to, or `null` when there's no request. */
	get requestedScreen() {
		return requestedScreen;
	},

	/** Request a switch to a screen, optionally carrying a one-shot payload for the target screen. */
	requestScreen(key: string, payload?: unknown) {
		requestedScreen = key;
		pendingPayload = payload;
	},

	/** Read (and clear) the payload handed to the target screen. A one-shot, non-reactive handoff,
	 *  so a later manual navigation to the same screen opens at its defaults. */
	consumePayload<T>(): T | undefined {
		const payload = pendingPayload as T | undefined;
		pendingPayload = undefined;
		return payload;
	},

	/** Clear the pending screen request once the shell has switched (leaves any payload for the
	 *  target screen to consume on mount). */
	clear() {
		requestedScreen = null;
	}
};
