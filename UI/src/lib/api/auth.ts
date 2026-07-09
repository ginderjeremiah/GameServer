import {
	clearTokens,
	getAccessToken,
	getAccessTokenExpiry,
	getRefreshToken,
	getTokens,
	setTokens,
	type StoredTokens
} from './token-store';

/**
 * Owns the JWT refresh lifecycle that the request and socket layers lean on. Two concerns live here:
 *
 *  - **Silent refresh.** Access tokens are short-lived (15 min); when one is about to expire (or a
 *    request comes back 401) we mint a new pair from the stored refresh token and carry on.
 *  - **Single-use refresh tokens.** The backend invalidates a refresh token the moment it is used and
 *    returns a new one, so two concurrent refreshes would race — the second would present an
 *    already-consumed token and fail, logging the user out. `refreshTokens` therefore collapses
 *    concurrent callers onto a single in-flight request.
 *
 * The refresh call deliberately uses `fetch` rather than `ApiRequest`: it must not pass back through
 * the 401-retry interceptor (that would recurse), and keeping it dependency-free avoids an import
 * cycle with `api-request`.
 */

/** Refresh the access token this many seconds before it actually expires, to absorb clock skew. */
const EXPIRY_LEEWAY_SECONDS = 30;

let inFlightRefresh: Promise<StoredTokens | null> | null = null;

const performRefresh = async (refreshToken: string): Promise<StoredTokens | null> => {
	try {
		const response = await fetch('/api/Login/Refresh', {
			method: 'POST',
			headers: { 'content-type': 'application/json' },
			body: JSON.stringify({ refreshToken })
		});

		if (!response.ok) {
			return null;
		}

		const body = (await response.json()) as { data?: Partial<StoredTokens> };
		const tokens = body.data;
		if (tokens?.accessToken && tokens?.refreshToken) {
			return { accessToken: tokens.accessToken, refreshToken: tokens.refreshToken };
		}
	} catch {
		// Network error — treat as a failed refresh.
	}

	return null;
};

/**
 * Exchanges the stored refresh token for a fresh pair and persists the rotated tokens. Concurrent
 * callers **within this tab** share a single request so the single-use refresh token is only
 * consumed once — but the token pair lives in shared `localStorage`, and two tabs are a supported
 * state (see `SocketReplaced`), so a failure here doesn't necessarily mean the presented token was
 * genuinely spent/revoked: another tab may have already raced it through a refresh of its own. Before
 * treating a failure as fatal, re-read storage — if it now holds a different refresh token than the
 * one just presented, that tab won the race and its rotated pair is the live session, so adopt it
 * instead of clearing a session that's still alive elsewhere. Only when storage still holds the same
 * token we presented (no other tab rotated it) is the session actually dead.
 */
export const refreshTokens = (): Promise<StoredTokens | null> => {
	if (inFlightRefresh) {
		return inFlightRefresh;
	}

	const refreshToken = getRefreshToken();
	if (!refreshToken) {
		return Promise.resolve(null);
	}

	inFlightRefresh = performRefresh(refreshToken)
		.then((tokens) => {
			if (tokens) {
				setTokens(tokens);
				return tokens;
			}

			const current = getRefreshToken();
			if (current !== null && current !== refreshToken) {
				return getTokens();
			}

			clearTokens();
			return null;
		})
		.finally(() => {
			inFlightRefresh = null;
		});

	return inFlightRefresh;
};

/**
 * Ensures a usable access token is available before a request or socket connection, refreshing
 * pre-emptively when the current token is missing or within the leeway window of expiring. Returns
 * the access token to use, or null when no valid token could be obtained.
 */
export const ensureValidAccessToken = async (): Promise<string | null> => {
	const expiry = getAccessTokenExpiry();
	const nowSeconds = Date.now() / 1000;

	if (expiry === null || expiry - EXPIRY_LEEWAY_SECONDS <= nowSeconds) {
		const refreshed = await refreshTokens();
		return refreshed?.accessToken ?? null;
	}

	return getAccessToken();
};

/**
 * Handles an unrecoverable auth failure (refresh exhausted): clears the stored tokens and returns the
 * user to the login screen. A full-page navigation tears down all in-memory game state, mirroring the
 * logout flow. The redirect is skipped when already on the login page to avoid a reload loop.
 */
export const handleAuthFailure = (): void => {
	clearTokens();
	if (typeof window !== 'undefined' && window.location.pathname !== '/') {
		window.location.href = '/';
	}
};
