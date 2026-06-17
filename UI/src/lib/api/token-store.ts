/**
 * Persists the JWT access/refresh token pair issued by the backend on login (and rotated on every
 * refresh) in local storage, so the session survives a page refresh. This is the single source of
 * truth for the current credentials — the request and socket layers read the access token from here
 * to authenticate, and the auth layer reads/rotates the refresh token.
 */
import { safeLocalStorage } from '$lib/common/local-storage';

export interface StoredTokens {
	accessToken: string;
	refreshToken: string;
}

const STORAGE_KEY = 'gameserver.auth-tokens';

/** The currently stored token pair, or null when the user is not logged in. */
export const getTokens = (): StoredTokens | null => {
	const raw = safeLocalStorage()?.getItem(STORAGE_KEY);
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
	safeLocalStorage()?.setItem(STORAGE_KEY, JSON.stringify(tokens));
};

/** Removes the stored token pair (logout, or after an unrecoverable auth failure). */
export const clearTokens = (): void => {
	safeLocalStorage()?.removeItem(STORAGE_KEY);
};

export const getAccessToken = (): string | null => getTokens()?.accessToken ?? null;

export const getRefreshToken = (): string | null => getTokens()?.refreshToken ?? null;

/**
 * Decodes a base64url-encoded string (the JWT segment encoding) to a UTF-8 string. base64url swaps
 * `+`/`/` for `-`/`_` and drops the `=` padding, so we restore both before `atob`, then UTF-8-decode
 * the raw bytes — `atob` alone would mangle any multi-byte claim (e.g. an accented username).
 */
const decodeBase64Url = (value: string): string => {
	const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
	const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
	const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
	return new TextDecoder().decode(bytes);
};

/**
 * Decodes the (unverified) payload of the stored JWT access token. Verifying the signature is the
 * server's job; the client only reads a couple of claims (expiry, roles) to drive pre-emptive
 * refresh and role-based UI gating. Returns null when there is no token, it is not a well-formed
 * JWT, or the payload can't be parsed.
 */
const decodeAccessTokenPayload = (): Record<string, unknown> | null => {
	const token = getAccessToken();
	if (!token) {
		return null;
	}

	const parts = token.split('.');
	if (parts.length !== 3) {
		return null;
	}

	try {
		return JSON.parse(decodeBase64Url(parts[1])) as Record<string, unknown>;
	} catch {
		return null;
	}
};

/**
 * Decodes the `exp` claim (seconds since the Unix epoch) from the stored access token. Used to
 * refresh the token pre-emptively, just before it expires, rather than waiting for a request to be
 * rejected with a 401.
 */
export const getAccessTokenExpiry = (): number | null => {
	const exp = decodeAccessTokenPayload()?.exp;
	return typeof exp === 'number' ? exp : null;
};

/**
 * Reads the role claim(s) from the stored access token. The backend issues one `role` claim per
 * granted role; the standard JWT serialization collapses a single role to a string and multiple
 * roles to an array, so both shapes are normalized to a string array here. Returns an empty array
 * when not logged in or the token carries no roles.
 */
export const getRoles = (): string[] => {
	const role = decodeAccessTokenPayload()?.role;
	if (typeof role === 'string') {
		return [role];
	}
	if (Array.isArray(role)) {
		return role.filter((r): r is string => typeof r === 'string');
	}
	return [];
};
