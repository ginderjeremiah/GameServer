/**
 * Persists the JWT access/refresh token pair issued by the backend on login (and rotated on every
 * refresh) in local storage, so the session survives a page refresh. This is the single source of
 * truth for the current credentials — the request and socket layers read the access token from here
 * to authenticate, and the auth layer reads/rotates the refresh token.
 */
export interface StoredTokens {
	accessToken: string;
	refreshToken: string;
}

const STORAGE_KEY = 'gameserver.auth-tokens';

/**
 * Returns local storage when it is available (it is absent during SSR), or null otherwise. Access is
 * wrapped in a try/catch because reading `localStorage` throws in some privacy modes.
 */
const storage = (): Storage | null => {
	try {
		return typeof localStorage !== 'undefined' ? localStorage : null;
	} catch {
		return null;
	}
};

/** The currently stored token pair, or null when the user is not logged in. */
export const getTokens = (): StoredTokens | null => {
	const raw = storage()?.getItem(STORAGE_KEY);
	if (!raw) {
		return null;
	}

	try {
		const parsed = JSON.parse(raw) as Partial<StoredTokens>;
		if (parsed.accessToken && parsed.refreshToken) {
			return { accessToken: parsed.accessToken, refreshToken: parsed.refreshToken };
		}
	} catch {
		// Malformed entry — treat as logged out.
	}

	return null;
};

/** Persists a freshly issued token pair, replacing any previous one. */
export const setTokens = (tokens: StoredTokens): void => {
	storage()?.setItem(STORAGE_KEY, JSON.stringify(tokens));
};

/** Removes the stored token pair (logout, or after an unrecoverable auth failure). */
export const clearTokens = (): void => {
	storage()?.removeItem(STORAGE_KEY);
};

export const getAccessToken = (): string | null => getTokens()?.accessToken ?? null;

export const getRefreshToken = (): string | null => getTokens()?.refreshToken ?? null;

/**
 * Decodes the `exp` claim (seconds since the Unix epoch) from the stored access token without
 * verifying its signature — that is the server's job. Used to refresh the token pre-emptively, just
 * before it expires, rather than waiting for a request to be rejected with a 401.
 */
export const getAccessTokenExpiry = (): number | null => {
	const token = getAccessToken();
	if (!token) {
		return null;
	}

	const parts = token.split('.');
	if (parts.length !== 3) {
		return null;
	}

	try {
		const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
		const payload = JSON.parse(atob(base64)) as { exp?: number };
		return typeof payload.exp === 'number' ? payload.exp : null;
	} catch {
		return null;
	}
};
