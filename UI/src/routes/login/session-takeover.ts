import { ApiRequest } from '$lib/api';
import { confirmModal } from '$stores';

export const ACTIVE_SESSION_TITLE = 'Active Session Detected';
export const ACTIVE_SESSION_BODY =
	"It looks like you're already signed in and playing in another tab or on another device. " +
	'Continuing here will disconnect that session. Do you want to continue?';

/**
 * Checks whether `playerId` already has a live game connection open elsewhere and, if so, asks the user
 * to confirm taking it over — called for the character a caller is *about* to enter (or switch to), so
 * it must run before that commit (binding the character rotates the token and, for an in-game switch,
 * tears down whichever character is currently live) rather than after (#1518): entering the game opens a
 * websocket, which the backend treats as the player's single allowed connection and uses to kick any
 * existing session (the `SocketReplaced` flow), and the switcher's own commit would otherwise destroy a
 * perfectly healthy current session before the user got a chance to decline.
 *
 * Returns `true` when the caller should proceed with the commit (no other session, or the user confirmed
 * the takeover). Returns `false` when the user declined — since this runs before any commit, nothing has
 * changed and the caller simply doesn't proceed. The check fails open: a non-200 response (e.g. a network
 * blip) proceeds rather than blocking a legitimate entry behind a best-effort warning.
 */
export const confirmSessionTakeover = async (playerId: number): Promise<boolean> => {
	const response = await new ApiRequest('Auth/ActiveSession').get({ playerId });
	if (response.status !== 200 || !response.data?.active) {
		return true;
	}

	return await confirmModal({
		title: ACTIVE_SESSION_TITLE,
		body: ACTIVE_SESSION_BODY,
		confirmLabel: 'Continue Here',
		cancelLabel: 'Cancel'
	});
};
