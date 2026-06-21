/* Session resume — decides where a fresh page load (a refresh, or a deep link) should land, restoring
   whatever it can from the persisted session and the reference-data cache along the way.

   The token pair (token-store) and the reference-data cache (reference-cache) both survive a refresh,
   so a warm return trip needs no more than a Login/Status check and a single reference-version check
   before dropping the player straight back into the game — no loading screen. The root layout drives
   this on boot; see docs/frontend.md. */

import { ApiRequest, getTokens, reportDeviceInfo } from '$lib/api';
import { playerManager } from './player/player-manager';
import { hydrateAllFromCache } from './reference-data';

/**
 * Where the boot gate should send the player:
 *  - `login`   — no stored tokens, or the session could not be restored.
 *  - `loading` — session restored, but at least one reference-data set must be (re)downloaded.
 *  - `game`    — session restored and every reference-data set was current in the cache.
 */
export type ResumeDestination = 'login' | 'loading' | 'game';

export async function resumeSession(): Promise<ResumeDestination> {
	if (!getTokens()) {
		return 'login';
	}

	if (!(await restorePlayer())) {
		return 'login';
	}

	// Player restored: skip the loading screen only when the whole reference-data cache is current.
	return (await hydrateAllFromCache()) ? 'game' : 'loading';
}

/**
 * Restores the player from the active session via Login/Status. Returns false when no usable session
 * is available (no/expired session, or the request failed), so the caller falls back to the login
 * screen. The request layer silently refreshes the access token and, on an unrecoverable auth failure,
 * clears the stored tokens and returns to login on its own.
 */
async function restorePlayer(): Promise<boolean> {
	try {
		const response = await new ApiRequest('Login/Status').get();
		if (response.status !== 200) {
			return false;
		}

		playerManager.initialize(response.data);
		// Fire-and-forget: refresh this device's capabilities on a resumed session too.
		void reportDeviceInfo();
		return true;
	} catch {
		// Network error / unparseable response — treat the session as unrestorable.
		return false;
	}
}

/**
 * Re-pulls the authoritative player aggregate (Login/Status) and re-initializes the player manager.
 * Used by the welcome-back gate to refresh the offline-reward-updated state — level, exp, stat points,
 * unlocked skills, inventory — before the game engine builds the live battler from it. Best-effort: a
 * failed refresh leaves the existing in-memory player intact rather than stranding the player at the gate.
 */
export async function refreshPlayer(): Promise<void> {
	try {
		const response = await new ApiRequest('Login/Status').get();
		if (response.status === 200) {
			playerManager.initialize(response.data);
		}
	} catch {
		// Swallow — the gate proceeds on the existing player; the next natural reload reconciles.
	}
}
