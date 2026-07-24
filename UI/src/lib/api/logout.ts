import { ApiRequest } from './api-request';
import { getRotatedRefreshToken } from './auth';
import { clearTokens } from './token-store';

/**
 * Ends the current session: revokes the refresh token on the server, clears the locally stored token
 * pair, and returns the user to the login screen.
 *
 * A full-page navigation (rather than a client-side route change) is used so all in-memory game
 * state — the engines, managers and websocket connection, which are module-level singletons — is torn
 * down. This mirrors the SocketReplaced handling in the engine. After the reload the login page finds
 * no stored tokens and keeps the user on the login screen.
 */
export const logout = async () => {
	// Settled before reading, so a refresh due to fire inside ApiRequest's own pre-emptive check
	// (execute -> ensureValidAccessToken) can't rotate the token out from under this read (#2386).
	const refreshToken = await getRotatedRefreshToken();
	try {
		if (refreshToken) {
			await new ApiRequest('Auth/Logout').post({ refreshToken });
		}
	} finally {
		clearTokens();
		window.location.href = '/';
	}
};
