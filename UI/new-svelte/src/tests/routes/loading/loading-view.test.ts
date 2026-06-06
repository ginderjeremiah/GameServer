import { describe, it, expect, vi, beforeEach } from 'vitest';

// Hoisted spies/state shared by the mocks below. `staticData` stands in for the
// reactive store; each test starts it empty so every set begins "pending".
const { goto, resolve, get, staticData } = vi.hoisted(() => ({
	goto: vi.fn(),
	resolve: vi.fn((p: string) => p),
	get: vi.fn(),
	staticData: {} as Record<string, unknown>
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve }));
vi.mock('$lib/api', () => ({ ApiRequest: { get } }));
vi.mock('$stores', () => ({ staticData }));

// The module keeps a process-wide in-flight cache; reset modules between tests
// so a key resolved in one test doesn't satisfy another.
const loadView = async () => {
	vi.resetModules();
	const mod = await import('$routes/loading/loading-view.svelte');
	return new mod.LoadingView();
};

const SETS = ['zones', 'enemies', 'items', 'skills', 'itemMods', 'attributes', 'challenges', 'challengeTypes'];

beforeEach(() => {
	vi.clearAllMocks();
	for (const k of Object.keys(staticData)) {
		delete staticData[k];
	}
	get.mockResolvedValue([]);
});

describe('LoadingView', () => {
	it('builds the eight-set manifest and loads everything to done', async () => {
		const view = await loadView();
		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.items.map((i) => i.key)).toEqual(SETS);
		expect(view.phase).toBe('done');
		expect(view.items.every((i) => i.status === 'done')).toBe(true);
		expect(view.completed).toBe(8);
		expect(view.progressPct).toBe(100);
		expect(get).toHaveBeenCalledTimes(8);
		// Each set populated its store slot.
		expect(staticData.zones).toEqual([]);
		expect(staticData.challengeTypes).toEqual([]);
	});

	it('skips straight to done without fetching when everything is cached', async () => {
		for (const k of SETS) {
			staticData[k] = [];
		}
		const view = await loadView();
		await view.start();

		expect(view.phase).toBe('done');
		expect(view.activeIndex).toBe(8);
		expect(get).not.toHaveBeenCalled();
	});

	it('enters the error phase on a failed fetch and recovers on retry', async () => {
		get.mockRejectedValueOnce(new Error('boom'));
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

	it('falls back to a generic message when a fetch rejects without an Error', async () => {
		get.mockRejectedValueOnce('nope');
		const view = await loadView();

		vi.useFakeTimers();
		const run = view.start();
		await vi.runAllTimersAsync();
		await run;
		vi.useRealTimers();

		expect(view.phase).toBe('error');
		expect(view.currentItem?.error).toBe('Network error — could not reach server.');
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
