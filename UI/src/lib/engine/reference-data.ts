/* Shared reference-data plumbing: the table of static sets the game needs (zones, enemies, items, …),
   each loaded over the authenticated socket via its `Get*` command and cached in local storage keyed
   by the backend-supplied content version (see `GetReferenceDataVersions`), so a refresh only
   re-downloads the sets whose version changed.

   Both consumers build on this single table: the loading screen's manifest view-model
   (`routes/loading/loading-view.svelte.ts`) and the silent session-resume path (`session.ts`). It
   therefore lives in `$lib` rather than under either route. */

import { fetchSocketData } from '$lib/api';
import type { ApiSocketCommandNoRequest, ApiSocketResponseTypes } from '$lib/api/types/api-socket-type-map';
import { staticData } from '$stores';
import { readReferenceCache, writeReferenceCache } from './reference-cache';

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
		// fetchSocketData throws on a socket error (including the transport's per-request timeout), so a
		// failed load surfaces to the loading screen's retry UI / the resume fallback rather than hanging.
		load: async () => write(await fetchSocketData(command)),
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
	),
	refSource(
		'proficiencies',
		'Proficiencies',
		'GetProficiencies',
		() => staticData.proficiencies,
		(d) => (staticData.proficiencies = d)
	),
	refSource(
		'paths',
		'Paths',
		'GetPaths',
		() => staticData.paths,
		(d) => (staticData.paths = d)
	),
	refSource(
		'classes',
		'Classes',
		'GetClasses',
		() => staticData.classes,
		(d) => (staticData.classes = d)
	)
];

/* The reference sets the create-character class picker needs before the main loading screen runs:
   the class catalogue plus the skills/items/attributes its kit preview resolves names and accents
   from. Character creation happens on the select screen, ahead of the `/loading` reference-data
   load, so the picker pulls these on demand. */
const CLASS_PICKER_KEYS = new Set(['classes', 'skills', 'items', 'attributes']);

/** Server-reported content version per set, keyed by its socket command. */
export type ReferenceVersions = Map<string, string>;

/**
 * Fetches the per-set content versions over the socket. Returns null — so the caller fetches every set
 * fresh / bypasses the cache — when the call fails, so a transient version-check error never serves
 * stale data.
 */
export async function fetchReferenceVersions(): Promise<ReferenceVersions | null> {
	try {
		const versions = await fetchSocketData('GetReferenceDataVersions');
		// A plain Map: this only drives load orchestration and is never rendered, so it needn't be reactive.
		return new Map(versions.map((v) => [v.command, v.version]));
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
		// Clear the entry once it settles (success or failure) so concurrent callers share the in-flight
		// promise, but a later call — e.g. the forced reload on session resume — re-invokes `run`.
		pending = run()
			.then(() => Math.round(performance.now() - start))
			.finally(() => pendingFetches.delete(key));
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

/**
 * Ensures the reference sets the create-character class picker renders from are in memory — the class
 * catalogue and the skills/items/attributes its kit preview resolves. Used by the select screen, which
 * runs before the main loading screen, so the picker has data without waiting for world entry.
 *
 * Reuses the same version-check/cache path as the loading screen: a set already loaded or current in
 * the cache is hydrated without a download; the rest are fetched and cached, so the later loading
 * screen serves them straight from cache. A failed fetch rejects, so the caller can surface it.
 */
export async function loadClassPickerData(): Promise<void> {
	const sources = REFERENCE_DATA.filter((source) => CLASS_PICKER_KEYS.has(source.key));
	const versions = await fetchReferenceVersions();
	await Promise.all(
		sources.map(async (source) => {
			if (versions && hydrateFromCacheIfCurrent(source, versions)) {
				return;
			}
			await dedupedFetch(source.key, source.load);
			cacheSet(source, versions);
		})
	);
}
