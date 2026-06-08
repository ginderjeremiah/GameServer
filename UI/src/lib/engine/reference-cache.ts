/**
 * Persists fetched reference-data sets (zones, enemies, items, …) in local storage, each tagged with
 * the content version the backend reported for it (see the `GetReferenceDataVersions` socket command).
 * On the next boot the loading screen compares the server's current version with the stored one and
 * re-downloads only the sets that actually changed, hydrating the rest straight from local storage.
 *
 * Caching is strictly best-effort: every access is guarded so a corrupted entry, a full quota, or an
 * unavailable store (SSR / privacy mode) degrades to a normal fetch rather than breaking the boot.
 */
export interface CachedReferenceData {
	version: string;
	data: unknown;
}

const KEY_PREFIX = 'gameserver.refdata.';

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

/** Reads the cached payload for a reference-data set, or null when absent/corrupted/unavailable. */
export const readReferenceCache = (key: string): CachedReferenceData | null => {
	const raw = storage()?.getItem(KEY_PREFIX + key);
	if (!raw) {
		return null;
	}

	try {
		const parsed = JSON.parse(raw) as Partial<CachedReferenceData>;
		if (typeof parsed.version === 'string' && 'data' in parsed) {
			return { version: parsed.version, data: parsed.data };
		}
	} catch {
		// Malformed entry — treat as a cache miss.
	}

	return null;
};

/** Persists a reference-data set under its content version. Silently no-ops if storage is full/unavailable. */
export const writeReferenceCache = (key: string, version: string, data: unknown): void => {
	try {
		storage()?.setItem(KEY_PREFIX + key, JSON.stringify({ version, data }));
	} catch {
		// Quota exceeded or storage unavailable — caching is best-effort, so fall back to in-memory only.
	}
};
