import { ApiRequest } from './api-request';
import { clearTokens, getRefreshToken } from './token-store';

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
	const refreshToken = getRefreshToken();
	try {
		if (refreshToken) {
			await new ApiRequest('Auth/Logout').post({ refreshToken });
		}
	} finally {
		clearTokens();
		window.location.href = '/';
	}
};
