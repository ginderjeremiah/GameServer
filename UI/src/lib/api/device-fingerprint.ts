/**
 * Computes and caches a device fingerprint — a hash of stable client-side signals — that the request
 * layer attaches to every authenticated request (header `X-Device-Fingerprint`) so the backend can key
 * connection tracking on the device (see issue #29). The hash is deterministic, so it is stable across
 * sessions; it is cached in memory and local storage to avoid recomputing.
 */

const STORAGE_KEY = 'gameserver.device-fingerprint';

interface NavigatorWithCapabilities extends Navigator {
	deviceMemory?: number;
}

let cached: string | undefined;
let pending: Promise<string | undefined> | null = null;

const storage = (): Storage | null => {
	try {
		return typeof localStorage !== 'undefined' ? localStorage : null;
	} catch {
		return null;
	}
};

/**
 * The stable client-side signals that make up the fingerprint. Kept deterministic (no
 * timestamps/randomness) so the same device produces the same hash across sessions.
 */
const fingerprintSignals = (): string => {
	const nav = navigator as NavigatorWithCapabilities;
	const timeZone = (() => {
		try {
			return Intl.DateTimeFormat().resolvedOptions().timeZone;
		} catch {
			return '';
		}
	})();

	return [
		nav.userAgent,
		nav.language,
		(nav.languages ?? []).join(','),
		nav.hardwareConcurrency ?? '',
		nav.deviceMemory ?? '',
		screen.width,
		screen.height,
		screen.colorDepth,
		timeZone,
		new Date().getTimezoneOffset()
	].join('|');
};

/**
 * Hashes the fingerprint signals with SHA-256, returning a hex string. Returns undefined when the Web
 * Crypto API is unavailable (e.g. a non-secure context), in which case the device simply isn't tracked.
 */
const hashFingerprint = async (input: string): Promise<string | undefined> => {
	if (!globalThis.crypto?.subtle) {
		return undefined;
	}

	try {
		const bytes = new TextEncoder().encode(input);
		const digest = await crypto.subtle.digest('SHA-256', bytes);
		return Array.from(new Uint8Array(digest))
			.map((b) => b.toString(16).padStart(2, '0'))
			.join('');
	} catch {
		return undefined;
	}
};

/** The cached fingerprint, read synchronously by the request layer. Undefined until first computed. */
export const getDeviceFingerprint = (): string | undefined => {
	if (cached) {
		return cached;
	}

	const stored = storage()?.getItem(STORAGE_KEY) ?? undefined;
	if (stored) {
		cached = stored;
	}

	return cached;
};

/**
 * Computes the device fingerprint once (caching it in memory and local storage) and returns it.
 * Concurrent callers share the single in-flight computation. Returns undefined when it can't be computed.
 */
export const ensureDeviceFingerprint = async (): Promise<string | undefined> => {
	const existing = getDeviceFingerprint();
	if (existing) {
		return existing;
	}

	if (typeof navigator === 'undefined') {
		return undefined;
	}

	pending ??= (async () => {
		const hash = await hashFingerprint(fingerprintSignals());
		if (hash) {
			cached = hash;
			try {
				storage()?.setItem(STORAGE_KEY, hash);
			} catch {
				// Local storage unavailable (private mode) — the in-memory cache still serves the session.
			}
		}
		pending = null;
		return hash;
	})();

	return pending;
};
