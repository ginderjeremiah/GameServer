import { describe, it, expect, vi, beforeEach } from 'vitest';

// Hoisted spies/state shared by the mocks below. `staticData` stands in for the
// reactive store; each test starts it empty so every set begins "pending".
const { goto, resolve, sendSocketCommand, staticData, readReferenceCache, writeReferenceCache } = vi.hoisted(() => ({
	goto: vi.fn(),
	resolve: vi.fn((p: string) => p),
	sendSocketCommand: vi.fn(),
	staticData: {} as Record<string, unknown>,
	readReferenceCache: vi.fn(),
	writeReferenceCache: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve }));
vi.mock('$lib/api', () => ({ apiSocket: { sendSocketCommand } }));
vi.mock('$stores', () => ({ staticData }));
vi.mock('$routes/loading/reference-cache', () => ({ readReferenceCache, writeReferenceCache }));

// The module keeps a process-wide in-flight cache; reset modules between tests
// so a key resolved in one test doesn't satisfy another.
const loadView = async () => {
	vi.resetModules();
	const mod = await import('$routes/loading/loading-view.svelte');
	return new mod.LoadingView();
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
	'statisticTypes'
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
	'GetStatisticTypes'
];

/** Builds the GetReferenceDataVersions payload, defaulting every set to "v1". */
const versionsResponse = (overrides: Record<string, string> = {}) => ({
	data: COMMANDS.map((command) => ({ command, version: overrides[command] ?? 'v1' }))
});

/** Versions resolve from the map; every data command resolves to an empty array. */
const respondWithVersions = (overrides: Record<string, string> = {}) => {
	sendSocketCommand.mockImplementation((command: string) => {
		if (command === 'GetReferenceDataVersions') {
			return Promise.resolve(versionsResponse(overrides));
		}
		return Promise.resolve({ data: [] });
	});
};

const dataCommandCalls = () =>
	sendSocketCommand.mock.calls.map((c) => c[0]).filter((name) => name !== 'GetReferenceDataVersions');

beforeEach(() => {
	vi.clearAllMocks();
	for (const k of Object.keys(staticData)) {
		delete staticData[k];
	}
	readReferenceCache.mockReturnValue(null);
	respondWithVersions();
});

describe('LoadingView', () => {
	it('builds the manifest and loads every set over the socket, caching each one', async () => {
		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.items.map((i) => i.key)).toEqual(SETS);
		expect(view.phase).toBe('done');
		expect(view.items.every((i) => i.status === 'done')).toBe(true);
		expect(view.completed).toBe(SETS.length);
		expect(view.progressPct).toBe(100);

		// The version check ran, then each set was fetched over its socket command.
		expect(sendSocketCommand).toHaveBeenCalledWith('GetReferenceDataVersions');
		expect(dataCommandCalls()).toEqual(COMMANDS);

		// Each freshly-loaded set was written to the cache under its server version.
		expect(writeReferenceCache).toHaveBeenCalledTimes(SETS.length);
		expect(writeReferenceCache).toHaveBeenCalledWith('zones', 'v1', []);
		expect(staticData.zones).toEqual([]);
		expect(staticData.statisticTypes).toEqual([]);
	});

	it('skips straight to done without any socket call when everything is already in memory', async () => {
		for (const k of SETS) {
			staticData[k] = [];
		}
		const view = await loadView();
		await view.start();

		expect(view.phase).toBe('done');
		expect(view.activeIndex).toBe(SETS.length);
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('hydrates from the cache and skips per-set fetches when versions match', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		const view = await loadView();
		await view.start();

		expect(view.phase).toBe('done');
		expect(view.items.every((i) => i.status === 'done')).toBe(true);

		// Only the version check went over the socket — no per-set fetch.
		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(dataCommandCalls()).toEqual([]);
		expect(staticData.zones).toEqual(['cached-zones']);
		expect(staticData.statisticTypes).toEqual(['cached-statisticTypes']);
		expect(writeReferenceCache).not.toHaveBeenCalled();
	});

	it('re-fetches only the sets whose version changed', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		respondWithVersions({ GetItems: 'v2' });

		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.phase).toBe('done');
		// Only the stale "items" set was fetched fresh; the rest hydrated from cache.
		expect(dataCommandCalls()).toEqual(['GetItems']);
		expect(staticData.items).toEqual([]);
		expect(staticData.zones).toEqual(['cached-zones']);
		// The refreshed set was re-cached under its new version.
		expect(writeReferenceCache).toHaveBeenCalledTimes(1);
		expect(writeReferenceCache).toHaveBeenCalledWith('items', 'v2', []);
	});

	it('loads everything fresh and bypasses the cache when the version check fails', async () => {
		readReferenceCache.mockImplementation((key: string) => ({ version: 'v1', data: [`cached-${key}`] }));
		sendSocketCommand.mockImplementation((command: string) => {
			if (command === 'GetReferenceDataVersions') {
				return Promise.reject(new Error('offline'));
			}
			return Promise.resolve({ data: [] });
		});

		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.phase).toBe('done');
		// Every set fetched fresh; the cache was neither read for hydration nor written (no versions).
		expect(dataCommandCalls()).toEqual(COMMANDS);
		expect(readReferenceCache).not.toHaveBeenCalled();
		expect(writeReferenceCache).not.toHaveBeenCalled();
		expect(staticData.zones).toEqual([]);
	});

	it('enters the error phase on a failed set load and recovers on retry', async () => {
		let failZonesOnce = true;
		sendSocketCommand.mockImplementation((command: string) => {
			if (command === 'GetReferenceDataVersions') {
				return Promise.resolve(versionsResponse());
			}
			if (command === 'GetZones' && failZonesOnce) {
				failZonesOnce = false;
				return Promise.reject(new Error('boom'));
			}
			return Promise.resolve({ data: [] });
		});

		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;

		expect(view.phase).toBe('error');
		expect(view.activeIndex).toBe(0);
		expect(view.currentItem?.error).toBe('boom');
		expect(view.items[0].status).toBe('error');
		// Loading stopped at the failed set — later sets are untouched.
		expect(view.items[1].status).toBe('pending');

		const retry = view.retryFailed();
		await vi.runAllTimersAsync();
		await retry;
		vi.useRealTimers();

		expect(view.phase).toBe('done');
		expect(view.items.every((i) => i.status === 'done')).toBe(true);
	});

	it('falls back to a generic message when a set load rejects without an Error', async () => {
		sendSocketCommand.mockImplementation((command: string) => {
			if (command === 'GetReferenceDataVersions') {
				return Promise.resolve(versionsResponse());
			}
			if (command === 'GetZones') {
				return Promise.reject('nope');
			}
			return Promise.resolve({ data: [] });
		});

		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.phase).toBe('error');
		expect(view.currentItem?.error).toBe('Network error — could not reach server.');
	});

	it('surfaces a timeout error when a set load never resolves', async () => {
		sendSocketCommand.mockImplementation((command: string) => {
			if (command === 'GetReferenceDataVersions') {
				return Promise.resolve(versionsResponse());
			}
			if (command === 'GetZones') {
				return new Promise(() => {}); // never settles
			}
			return Promise.resolve({ data: [] });
		});

		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync(); // advances past the socket timeout
		await run;
		vi.useRealTimers();

		expect(view.phase).toBe('error');
		expect(view.currentItem?.error).toBe('Timed out waiting for the server.');
	});

	it('navigates to the game only once loading is done', async () => {
		const view = await loadView();

		view.enterGame();
		expect(goto).not.toHaveBeenCalled();

		view.phase = 'done';
		view.enterGame();
		expect(resolve).toHaveBeenCalledWith('/game');
		expect(goto).toHaveBeenCalledTimes(1);
	});
});
