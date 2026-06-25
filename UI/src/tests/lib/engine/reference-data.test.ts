import { describe, it, expect, vi, beforeEach } from 'vitest';

// Hoisted spies/state shared by the mocks below. `staticData` stands in for the reactive store; each
// test starts it empty so every set begins "not loaded".
const { sendSocketCommand, staticData, readReferenceCache, writeReferenceCache } = vi.hoisted(() => ({
	sendSocketCommand: vi.fn(),
	staticData: {} as Record<string, unknown>,
	readReferenceCache: vi.fn(),
	writeReferenceCache: vi.fn()
}));

// reference-data drives the socket through fetchSocketData, which throws on a socket error. The mock
// delegates to the per-test sendSocketCommand spy so each test still configures behaviour the same way.
vi.mock('$lib/api', () => ({
	fetchSocketData: async (command: string) => {
		const response = await sendSocketCommand(command);
		if (response.error) {
			throw new Error(response.error);
		}
		return response.data;
	}
}));
vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/engine/reference-cache', () => ({ readReferenceCache, writeReferenceCache }));

// The module keeps a process-wide in-flight cache (dedupedFetch); reset modules between tests so a key
// resolved in one test doesn't satisfy another.
const loadModule = async () => {
	vi.resetModules();
	return import('$lib/engine/reference-data');
};

const SETS = [
	'zones',
	'enemies',
	'items',
	'skills',
	'itemMods',
	'attributes',
	'challenges',
	'challengeTypes',
	'statisticTypes',
	'proficiencies',
	'paths',
	'classes'
];

const COMMANDS = [
	'GetZones',
	'GetEnemies',
	'GetItems',
	'GetSkills',
	'GetItemMods',
	'GetAttributes',
	'GetChallenges',
	'GetChallengeTypes',
	'GetStatisticTypes',
	'GetProficiencies',
	'GetPaths',
	'GetClasses'
];

/** Builds the GetReferenceDataVersions payload, defaulting every set to "v1". */
const versionsResponse = (overrides: Record<string, string> = {}) => ({
	data: COMMANDS.map((command) => ({ command, version: overrides[command] ?? 'v1' }))
});

const respondWithVersions = (overrides: Record<string, string> = {}) => {
	sendSocketCommand.mockImplementation((command: string) => {
		if (command === 'GetReferenceDataVersions') {
			return Promise.resolve(versionsResponse(overrides));
		}
		return Promise.resolve({ data: [] });
	});
};

beforeEach(() => {
	vi.clearAllMocks();
	for (const k of Object.keys(staticData)) {
		delete staticData[k];
	}
	readReferenceCache.mockReturnValue(null);
	respondWithVersions();
});

describe('hydrateAllFromCache', () => {
	it('returns true without any socket call when every set is already in memory', async () => {
		for (const k of SETS) {
			staticData[k] = [];
		}
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(true);
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('hydrates every set from a current cache and returns true', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(true);
		// Only the version check went over the socket — nothing was downloaded.
		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(sendSocketCommand).toHaveBeenCalledWith('GetReferenceDataVersions');
		expect(staticData.zones).toEqual(['cached-zones']);
		expect(staticData.statisticTypes).toEqual(['cached-statisticTypes']);
	});

	it('returns false and commits nothing when a single set is missing from the cache', async () => {
		// Every set is cached except "items".
		readReferenceCache.mockImplementation((key: string) =>
			key === 'items' ? null : { version: 'v1', data: [`cached-${key}`] }
		);
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(false);
		// Atomic: because one set could not be resolved, none of the others were committed to the store.
		for (const k of SETS) {
			expect(staticData[k]).toBeUndefined();
		}
	});

	it('returns false when a set has a stale cached version', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		respondWithVersions({ GetItems: 'v2' });
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(false);
		expect(staticData.zones).toBeUndefined();
	});

	it('returns false without reading the cache when the version check fails', async () => {
		sendSocketCommand.mockImplementation((command: string) =>
			command === 'GetReferenceDataVersions' ? Promise.reject(new Error('offline')) : Promise.resolve({ data: [] })
		);
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(false);
		expect(readReferenceCache).not.toHaveBeenCalled();
	});

	it('returns false when the server omits a set from the versions response', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		// The server reports versions for every set except GetZones.
		sendSocketCommand.mockImplementation((command: string) => {
			if (command === 'GetReferenceDataVersions') {
				return Promise.resolve({
					data: COMMANDS.filter((c) => c !== 'GetZones').map((c) => ({ command: c, version: 'v1' }))
				});
			}
			return Promise.resolve({ data: [] });
		});
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(false);
	});

	it('only fetches versions once and only needs the sets not already in memory', async () => {
		// All sets but "zones" are already in memory; "zones" resolves from a current cache.
		for (const k of SETS.filter((s) => s !== 'zones')) {
			staticData[k] = ['in-memory'];
		}
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		const { hydrateAllFromCache } = await loadModule();

		expect(await hydrateAllFromCache()).toBe(true);
		expect(readReferenceCache).toHaveBeenCalledTimes(1);
		expect(readReferenceCache).toHaveBeenCalledWith('zones');
		expect(staticData.zones).toEqual(['cached-zones']);
	});
});

describe('fetchReferenceVersions', () => {
	it('returns a command→version map on success', async () => {
		respondWithVersions({ GetItems: 'v9' });
		const { fetchReferenceVersions } = await loadModule();

		const versions = await fetchReferenceVersions();
		expect(versions?.get('GetZones')).toBe('v1');
		expect(versions?.get('GetItems')).toBe('v9');
	});

	it('returns null when the version check rejects', async () => {
		sendSocketCommand.mockRejectedValue(new Error('boom'));
		const { fetchReferenceVersions } = await loadModule();

		expect(await fetchReferenceVersions()).toBeNull();
	});
});

describe('hydrateFromCacheIfCurrent', () => {
	it('returns true and skips the cache when the set is already loaded', async () => {
		staticData.zones = ['in-memory'];
		const { REFERENCE_DATA, hydrateFromCacheIfCurrent } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		expect(hydrateFromCacheIfCurrent(zones, new Map([['GetZones', 'v1']]))).toBe(true);
		expect(readReferenceCache).not.toHaveBeenCalled();
	});

	it('hydrates and returns true when the cached version matches', async () => {
		readReferenceCache.mockReturnValue({ version: 'v1', data: ['cached'] });
		const { REFERENCE_DATA, hydrateFromCacheIfCurrent } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		expect(hydrateFromCacheIfCurrent(zones, new Map([['GetZones', 'v1']]))).toBe(true);
		expect(staticData.zones).toEqual(['cached']);
	});

	it('returns false when the server reports no version for the set', async () => {
		const { REFERENCE_DATA, hydrateFromCacheIfCurrent } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		expect(hydrateFromCacheIfCurrent(zones, new Map())).toBe(false);
		expect(readReferenceCache).not.toHaveBeenCalled();
	});

	it('returns false when the cached version is stale', async () => {
		readReferenceCache.mockReturnValue({ version: 'v1', data: ['cached'] });
		const { REFERENCE_DATA, hydrateFromCacheIfCurrent } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		expect(hydrateFromCacheIfCurrent(zones, new Map([['GetZones', 'v2']]))).toBe(false);
		expect(staticData.zones).toBeUndefined();
	});
});

describe('cacheSet', () => {
	it('writes the current in-memory value under the server version', async () => {
		staticData.zones = ['fresh'];
		const { REFERENCE_DATA, cacheSet } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		cacheSet(zones, new Map([['GetZones', 'v3']]));
		expect(writeReferenceCache).toHaveBeenCalledWith('zones', 'v3', ['fresh']);
	});

	it('does nothing when the set has no known server version', async () => {
		const { REFERENCE_DATA, cacheSet } = await loadModule();
		const zones = REFERENCE_DATA.find((s) => s.key === 'zones')!;

		cacheSet(zones, null);
		cacheSet(zones, new Map());
		expect(writeReferenceCache).not.toHaveBeenCalled();
	});
});

describe('dedupedFetch', () => {
	it('shares a single in-flight promise for the same key', async () => {
		const { dedupedFetch } = await loadModule();
		const run = vi.fn(() => new Promise<void>((resolve) => setTimeout(resolve, 0)));

		const a = dedupedFetch('zones', run);
		const b = dedupedFetch('zones', run);
		expect(a).toBe(b);
		await a;
		expect(run).toHaveBeenCalledTimes(1);
	});

	it('clears the in-flight entry on success so a later call re-runs', async () => {
		const { dedupedFetch } = await loadModule();
		const run = vi.fn().mockResolvedValue(undefined);

		// First load resolves and clears the entry...
		await expect(dedupedFetch('zones', run)).resolves.toBeTypeOf('number');
		// ...so a forced reload re-invokes `run` rather than returning the stale resolved promise.
		await expect(dedupedFetch('zones', run)).resolves.toBeTypeOf('number');
		expect(run).toHaveBeenCalledTimes(2);
	});

	it('clears the in-flight entry on failure so a later call can retry', async () => {
		const { dedupedFetch } = await loadModule();
		const run = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce(undefined);

		await expect(dedupedFetch('zones', run)).rejects.toThrow('boom');
		await expect(dedupedFetch('zones', run)).resolves.toBeTypeOf('number');
		expect(run).toHaveBeenCalledTimes(2);
	});
});
