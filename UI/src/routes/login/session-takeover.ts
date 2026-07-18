import { ApiRequest, logout } from '$lib/api';
import { confirmModal } from '$stores';

export const ACTIVE_SESSION_TITLE = 'Active Session Detected';
export const ACTIVE_SESSION_BODY =
	"It looks like you're already signed in and playing in another tab or on another device. " +
	'Continuing here will disconnect that session. Do you want to continue?';

/**
 * After authenticating, checks whether the player already has a live game connection open elsewhere
 * and, if so, asks the user to confirm taking it over before entering the game.
 *
 * Entering the game opens a websocket, which the backend treats as the player's single allowed
 * connection and uses to kick any existing session (the `SocketReplaced` flow). This check runs first
 * — over HTTP, so it never opens a socket of its own — and lets the user decide whether to claim the
 * session here.
 *
 * Returns `true` when the caller should proceed into the game (no other session, or the user confirmed
 * the takeover). Returns `false` when the user declined; in that case the just-issued session is logged
 * out so the other session is left untouched. The check fails open: a non-200 response (e.g. a network
 * blip) proceeds rather than blocking a legitimate login behind a best-effort warning.
 */
export const confirmSessionTakeover = async (): Promise<boolean> => {
	const response = await new ApiRequest('Auth/ActiveSession').get();
	if (response.status !== 200 || !response.data?.active) {
		return true;
	}

	const proceed = await confirmModal({
		title: ACTIVE_SESSION_TITLE,
		body: ACTIVE_SESSION_BODY,
		confirmLabel: 'Continue Here',
		cancelLabel: 'Cancel'
	});

	if (!proceed) {
		await logout();
	}

	return proceed;
};
