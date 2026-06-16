/* Import this directly (`$lib/common/local-storage`), not via the `$lib/common` barrel: the barrel
   transitively imports `$lib/api` (through `rarity`/`challenge-type`), so an api-layer consumer such as
   the token store or device fingerprint pulling it through the barrel would form an import cycle. It is
   intentionally left out of the barrel so that deep import can't be "tidied" back into a barrel import. */

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
