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

/**
 * In-memory shadow of the stored token pair. Storage is normally the source of truth for reads (so a
 * pair rotated by another tab is picked up on the next read — see `auth.ts`'s multi-tab handling), but
 * `setTokens` writes through to this mirror first. When the storage write itself then fails (quota
 * exceeded, or storage blocked outright), reads fall back to the mirror instead of a stale/absent
 * storage entry, so a freshly rotated single-use token isn't lost for the rest of this tab's session.
 */
let memoryTokens: StoredTokens | null = null;
let storageWriteFailed = false;

const parseStoredTokens = (raw: string): StoredTokens | null => {
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

/** The currently stored token pair, or null when the user is not logged in. */
export const getTokens = (): StoredTokens | null => {
	const storage = safeLocalStorage();
	if (storage && !storageWriteFailed) {
		try {
			const raw = storage.getItem(STORAGE_KEY);
			memoryTokens = raw ? parseStoredTokens(raw) : null;
			return memoryTokens;
		} catch {
			// Reading storage threw — fall back to the in-memory mirror below.
		}
	}

	return memoryTokens;
};

/** Persists a freshly issued token pair, replacing any previous one. */
export const setTokens = (tokens: StoredTokens): void => {
	memoryTokens = tokens;

	const storage = safeLocalStorage();
	if (!storage) {
		storageWriteFailed = true;
		return;
	}

	try {
		storage.setItem(STORAGE_KEY, JSON.stringify(tokens));
		storageWriteFailed = false;
	} catch {
		// Quota exceeded or storage blocked — the in-memory mirror keeps this tab's session usable
		// until a write succeeds again; it won't survive a reload.
		storageWriteFailed = true;
	}
};

/** Removes the stored token pair (logout, or after an unrecoverable auth failure). */
export const clearTokens = (): void => {
	memoryTokens = null;
	storageWriteFailed = false;
	try {
		safeLocalStorage()?.removeItem(STORAGE_KEY);
	} catch {
		// Best-effort — nothing else to reasonably do if this throws.
	}
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
