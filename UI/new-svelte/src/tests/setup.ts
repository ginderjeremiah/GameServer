// Vitest setup, run once per test file before the suite (see `setupFiles` in vite.config.ts).
//
// The unit suite runs in the jsdom environment, which provides working `localStorage` /
// `sessionStorage`. Node 22.4+ (e.g. Node 25, which some contributors run locally) ships its
// own global Web Storage that is inert unless started with `--localstorage-file`, and it
// shadows jsdom's copy on `globalThis` — so `localStorage.clear()` throws "is not a function".
// CI runs an older Node without that global, which is why the api/token-store tests only fail
// locally. When the global Storage is the broken native one, swap in a minimal in-memory
// implementation so behaviour matches CI on every Node version. On older Node / CI this is a
// no-op because jsdom's Storage is already present and functional.

function inMemoryStorage(): Storage {
	const map = new Map<string, string>();
	return {
		get length() {
			return map.size;
		},
		clear() {
			map.clear();
		},
		getItem(key: string) {
			return map.has(key) ? map.get(key)! : null;
		},
		setItem(key: string, value: string) {
			map.set(String(key), String(value));
		},
		removeItem(key: string) {
			map.delete(key);
		},
		key(index: number) {
			return Array.from(map.keys())[index] ?? null;
		}
	} as Storage;
}

for (const name of ['localStorage', 'sessionStorage'] as const) {
	const current = (globalThis as Record<string, unknown>)[name] as Storage | undefined;
	if (!current || typeof current.clear !== 'function') {
		Object.defineProperty(globalThis, name, {
			value: inMemoryStorage(),
			configurable: true,
			writable: true
		});
	}
}
