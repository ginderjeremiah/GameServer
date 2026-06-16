/**
 * Returns local storage when it is available (it is absent during SSR), or null otherwise. Access is
 * wrapped in a try/catch because reading `localStorage` throws in some privacy modes. This is the single
 * best-effort guard the token store, reference cache, and device fingerprint all share, so persistence
 * degrades gracefully to in-memory only rather than throwing.
 */
export const safeLocalStorage = (): Storage | null => {
	try {
		return typeof localStorage !== 'undefined' ? localStorage : null;
	} catch {
		return null;
	}
};
