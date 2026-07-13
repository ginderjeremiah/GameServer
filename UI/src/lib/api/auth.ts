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

/**
 * A refresh attempt's outcome, distinguishing a definitive rejection from a merely failed attempt:
 *
 *  - `success` — a fresh token pair was minted (or another tab's rotated pair was adopted).
 *  - `rejected` — either there is no stored refresh token to present, or the backend affirmatively
 *    rejected the one presented (`Login/Refresh` returns 400 for "Invalid or expired refresh token" —
 *    its only failure path). Both are a definitively dead session, not something a retry can fix.
 *  - `retryable` — the attempt didn't complete (network error, a non-400 non-2xx status such as a 5xx or
 *    a 429 from rate limiting, or a malformed body). The stored refresh token may still be good; this is
 *    not proof the session is dead, so callers must not clear tokens or force a logout on it.
 */
export type RefreshOutcome =
	| { status: 'success'; tokens: StoredTokens }
	| { status: 'rejected' }
	| { status: 'retryable' };

/** The access token to use (or null when none could be obtained) alongside whether a refresh attempt —
 *  if one was made — was a definitive rejection, so callers can tell a dead session from a transient one. */
export type AccessTokenResult = { accessToken: string | null; rejected: boolean };

let inFlightRefresh: Promise<RefreshOutcome> | null = null;

const performRefresh = async (refreshToken: string): Promise<RefreshOutcome> => {
	let response: Response;
	try {
		response = await fetch('/api/Login/Refresh', {
			method: 'POST',
			headers: { 'content-type': 'application/json' },
			body: JSON.stringify({ refreshToken })
		});
	} catch {
		// Network error — can't tell whether the token is still valid, so it isn't a rejection.
		return { status: 'retryable' };
	}

	if (!response.ok) {
		return response.status === 400 ? { status: 'rejected' } : { status: 'retryable' };
	}

	let body: { data?: Partial<StoredTokens> };
	try {
		body = (await response.json()) as { data?: Partial<StoredTokens> };
	} catch {
		// A 2xx whose body isn't parseable JSON (a proxy/captive-portal interception, a truncated
		// response) is a transient-intermediary failure, not proof the refresh token is dead.
		return { status: 'retryable' };
	}

	const tokens = body.data;
	if (tokens?.accessToken && tokens?.refreshToken) {
		return { status: 'success', tokens: { accessToken: tokens.accessToken, refreshToken: tokens.refreshToken } };
	}

	// A 2xx with no usable token pair is a malformed response, not proof the refresh token is dead.
	return { status: 'retryable' };
};

/**
 * Exchanges the stored refresh token for a fresh pair and persists the rotated tokens. Concurrent
 * callers **within this tab** share a single request so the single-use refresh token is only consumed
 * once — but the token pair lives in shared `localStorage`, and two tabs are a supported state (see
 * `SocketReplaced`), so a failed attempt is re-checked against storage before it stands: if another
 * tab has since rotated in a different refresh token, that tab won the race (spending the token this
 * one presented) and its rotated pair is the live session, so it is adopted as a `success` instead of
 * failing a session that's still alive elsewhere. Stored tokens are cleared only on a definitive
 * rejection with no adoptable pair — a retryable failure (network blip, transient server error) leaves
 * them in place so a later attempt can still use the still-possibly-valid refresh token.
 */
export const refreshTokens = (): Promise<RefreshOutcome> => {
	if (inFlightRefresh) {
		return inFlightRefresh;
	}

	const refreshToken = getRefreshToken();
	if (!refreshToken) {
		return Promise.resolve({ status: 'rejected' });
	}

	inFlightRefresh = performRefresh(refreshToken)
		.then((outcome): RefreshOutcome => {
			if (outcome.status === 'success') {
				setTokens(outcome.tokens);
				return outcome;
			}

			// A failure isn't necessarily ours: another tab may have raced this one through a refresh
			// (spending the token we presented reads as a 400 rejection here). If storage now holds a
			// different refresh token than the one presented, adopt that rotated pair.
			const current = getTokens();
			if (current !== null && current.refreshToken !== refreshToken) {
				return { status: 'success', tokens: current };
			}

			if (outcome.status === 'rejected') {
				clearTokens();
			}

			return outcome;
		})
		.finally(() => {
			inFlightRefresh = null;
		});

	return inFlightRefresh;
};

/**
 * Ensures a usable access token is available before a request or socket connection, refreshing
 * pre-emptively when the current token is missing or within the leeway window of expiring.
 */
export const ensureValidAccessToken = async (): Promise<AccessTokenResult> => {
	const expiry = getAccessTokenExpiry();
	const nowSeconds = Date.now() / 1000;

	if (expiry === null || expiry - EXPIRY_LEEWAY_SECONDS <= nowSeconds) {
		const outcome = await refreshTokens();
		return {
			accessToken: outcome.status === 'success' ? outcome.tokens.accessToken : null,
			rejected: outcome.status === 'rejected'
		};
	}

	return { accessToken: getAccessToken(), rejected: false };
};

/**
 * Handles an unrecoverable auth failure (refresh definitively rejected): clears the stored tokens and
 * returns the user to the login screen. A full-page navigation tears down all in-memory game state,
 * mirroring the logout flow. The redirect is skipped when already on the login page to avoid a reload loop.
 *
 * This always clears — a caller reacting to a rejected refresh must re-read storage itself first (as
 * `execute`/`openSocket`/`handleClose` do) and skip calling this at all if a concurrent tab has since
 * rotated in a fresh pair, otherwise this unconditional clear would wipe out a session that's still
 * alive elsewhere.
 */
export const handleAuthFailure = (): void => {
	clearTokens();
	if (typeof window !== 'undefined' && window.location.pathname !== '/') {
		window.location.href = '/';
	}
};
