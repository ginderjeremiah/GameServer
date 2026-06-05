import { ApiRequest } from './api-request';

/**
 * Ends the current session: clears the server-side auth cookie and returns the
 * user to the login screen.
 *
 * A full-page navigation (rather than a client-side route change) is used so all
 * in-memory game state — the engines, managers and websocket connection, which
 * are module-level singletons — is torn down. This mirrors the SocketReplaced
 * handling in the engine. After the reload the login page's session check finds
 * no valid cookie and keeps the user on the login screen.
 */
export const logout = async () => {
	await new ApiRequest('Login/Logout').post();
	window.location.href = '/';
};
