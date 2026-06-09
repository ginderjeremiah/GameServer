/* Shared reference-data plumbing: the table of static sets the game needs (zones, enemies, items, …),
   each loaded over the authenticated socket via its `Get*` command and cached in local storage keyed
   by the backend-supplied content version (see `GetReferenceDataVersions`), so a refresh only
   re-downloads the sets whose version changed.

   Both consumers build on this single table: the loading screen's manifest view-model
   (`routes/loading/loading-view.svelte.ts`) and the silent session-resume path (`session.ts`). It
   therefore lives in `$lib` rather than under either route. */

import { apiSocket } from '$lib/api';
import type { ApiSocketCommandNoRequest, ApiSocketResponseTypes } from '$lib/api/types/api-socket-type-map';
import { staticData } from '$stores';
import { readReferenceCache, writeReferenceCache } from './reference-cache';

/* The socket has no built-in per-command timeout; without one a dead or unreachable backend would
   leave a caller hanging on a command that never resolves. Bounding each socket call surfaces the
   error to the caller (the loading screen's retry UI, or a resume falling back) instead of hanging. */
const SOCKET_TIMEOUT_MS = 15000;

export const withTimeout = <T>(promise: Promise<T>): Promise<T> =>
	new Promise<T>((resolve, reject) => {
		const timer = setTimeout(() => reject(new Error('Timed out waiting for the server.')), SOCKET_TIMEOUT_MS);
		promise.then(
			(value) => {
				clearTimeout(timer);
				resolve(value);
			},
			(error) => {
				clearTimeout(timer);
				reject(error);
			}
		);
	});

/* One row per reference-data set. Keeping them in a single table keeps both the manifest and the
   resume check DRY — adding/removing a set is a one-line edit here. */
export interface RefDataSource {
	key: string;
	label: string;
	// The socket command that loads this set; also the key its version is reported under.
	command: ApiSocketCommandNoRequest;
	// Whether the in-memory store slot is already populated (so a same-session re-mount can skip it).
	loaded: () => boolean;
	// Fetch over the socket and populate the in-memory store.
	load: () => Promise<void>;
	// Populate the in-memory store from a cached payload.
	hydrate: (data: unknown) => void;
	// The current in-memory value, to write to the cache after a fresh load.
	current: () => unknown;
}

/* Builds a typed reference-data source from a store getter/setter and its socket command, so each row
   stays type-checked against the command's response type. */
function refSource<C extends ApiSocketCommandNoRequest>(
	key: string,
	label: string,
	command: C,
	read: () => ApiSocketResponseTypes[C] | undefined,
	write: (data: ApiSocketResponseTypes[C]) => void
): RefDataSource {
	return {
		key,
		label,
		command,
		loaded: () => read() != null,
		load: async () => write((await withTimeout(apiSocket.sendSocketCommand(command))).data),
		hydrate: (data) => write(data as ApiSocketResponseTypes[C]),
		current: () => read()
	};
}

export const REFERENCE_DATA: RefDataSource[] = [
	refSource(
		'zones',
		'Zones',
		'GetZones',
		() => staticData.zones,
		(d) => (staticData.zones = d)
	),
	refSource(
		'enemies',
		'Enemies',
		'GetEnemies',
		() => staticData.enemies,
		(d) => (staticData.enemies = d)
	),
	refSource(
		'items',
		'Items',
		'GetItems',
		() => staticData.items,
		(d) => (staticData.items = d)
	),
	refSource(
		'skills',
		'Skills',
		'GetSkills',
		() => staticData.skills,
		(d) => (staticData.skills = d)
	),
	refSource(
		'itemMods',
		'Item Mods',
		'GetItemMods',
		() => staticData.itemMods,
		(d) => (staticData.itemMods = d)
	),
	refSource(
		'attributes',
		'Attributes',
		'GetAttributes',
		() => staticData.attributes,
		(d) => (staticData.attributes = d)
	),
	refSource(
		'challenges',
		'Challenges',
		'GetChallenges',
		() => staticData.challenges,
		(d) => (staticData.challenges = d)
	),
	refSource(
		'challengeTypes',
		'Challenge Types',
		'GetChallengeTypes',
		() => staticData.challengeTypes,
		(d) => (staticData.challengeTypes = d)
	),
	refSource(
		'statisticTypes',
		'Statistic Types',
		'GetStatisticTypes',
		() => staticData.statisticTypes,
		(d) => (staticData.statisticTypes = d)
	)
];

/** Server-reported content version per set, keyed by its socket command. */
export type ReferenceVersions = Map<string, string>;

/**
 * Fetches the per-set content versions over the socket. Returns null — so the caller fetches every set
 * fresh / bypasses the cache — when the call fails, so a transient version-check error never serves
 * stale data.
 */
export async function fetchReferenceVersions(): Promise<ReferenceVersions | null> {
	try {
		const response = await withTimeout(apiSocket.sendSocketCommand('GetReferenceDataVersions'));
		// A plain Map: this only drives load orchestration and is never rendered, so it needn't be reactive.
		return new Map(response.data.map((v) => [v.command, v.version]));
	} catch {
		return null;
	}
}

/**
 * Hydrates a single set from the local-storage cache iff its cached version matches the server's.
 * Returns true when the set is now in memory (already loaded, or hydrated from a current cache).
 */
export function hydrateFromCacheIfCurrent(source: RefDataSource, versions: ReferenceVersions): boolean {
	if (source.loaded()) {
		return true;
	}

	const serverVersion = versions.get(source.command);
	if (serverVersion == null) {
		return false;
	}

	const cached = readReferenceCache(source.key);
	if (cached && cached.version === serverVersion) {
		source.hydrate(cached.data);
		return true;
	}

	return false;
}

/** Persists a freshly-loaded set under the version the server reported for it (a no-op when unknown). */
export function cacheSet(source: RefDataSource, versions: ReferenceVersions | null): void {
	const version = versions?.get(source.command);
	if (version != null) {
		writeReferenceCache(source.key, version, source.current());
	}
}

// De-duplicates concurrent fetches of the same set (e.g. a re-mount mid-load) so any one key is only
// ever in flight once. A plain Map: it only drives load orchestration and is never rendered.
const pendingFetches = new Map<string, Promise<number>>();

/** Runs `run` for `key` at most once concurrently, timing the call in ms. */
export const dedupedFetch = (key: string, run: () => Promise<void>): Promise<number> => {
	let pending = pendingFetches.get(key);
	if (!pending) {
		const start = performance.now();
		pending = run()
			.then(() => Math.round(performance.now() - start))
			.catch((e) => {
				pendingFetches.delete(key);
				throw e;
			});
		pendingFetches.set(key, pending);
	}
	return pending;
};

/**
 * Attempts to satisfy every reference-data set from the in-memory store or the local-storage cache,
 * without downloading anything — the basis for resuming straight into the game on a refresh.
 *
 * Resolution is all-or-nothing: cached sets are committed to memory only when *every* set is present
 * and current. Otherwise the store is left untouched and it returns false, so the caller can fall back
 * to the loading screen (which re-checks versions and downloads the stale/missing sets as usual).
 * Returns false when the version check can't be reached, so a transient error never resumes against
 * stale data.
 */
export async function hydrateAllFromCache(): Promise<boolean> {
	if (REFERENCE_DATA.every((source) => source.loaded())) {
		return true;
	}

	const versions = await fetchReferenceVersions();
	if (!versions) {
		return false;
	}

	// Verify every not-yet-loaded set has a current cached copy before committing any of them.
	const pending: { source: RefDataSource; data: unknown }[] = [];
	for (const source of REFERENCE_DATA) {
		if (source.loaded()) {
			continue;
		}

		const serverVersion = versions.get(source.command);
		const cached = serverVersion != null ? readReferenceCache(source.key) : null;
		if (!cached || cached.version !== serverVersion) {
			return false;
		}

		pending.push({ source, data: cached.data });
	}

	for (const { source, data } of pending) {
		source.hydrate(data);
	}

	return true;
}
