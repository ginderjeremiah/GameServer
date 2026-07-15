import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { IOfflineProgressModel } from '$lib/api';
import { WelcomeBackView, type WelcomeBackDeps } from '$routes/game/welcome-back/welcome-back-view.svelte';

const progress = (overrides: Partial<IOfflineProgressModel> = {}): IOfflineProgressModel => ({
	awayMs: 3_600_000,
	autoChallengeBoss: false,
	zoneId: 1,
	battlesWon: 10,
	battlesLost: 2,
	battlesDrawn: 0,
	totalExp: 5000,
	levelsGained: 1,
	statPointsGained: 5,
	hasProgress: true,
	completedChallenges: [],
	proficiencyGains: [],
	openedProficiencies: [],
	...overrides
});

let deps: { [K in keyof WelcomeBackDeps]: ReturnType<typeof vi.fn> };

const makeView = (fetchResult: IOfflineProgressModel | null) => {
	deps = {
		fetchProgress: vi.fn(() => Promise.resolve(fetchResult)),
		resyncPlayer: vi.fn(() => Promise.resolve()),
		reconcileMode: vi.fn(),
		enterGame: vi.fn()
	};
	return new WelcomeBackView(deps as unknown as WelcomeBackDeps);
};

beforeEach(() => {
	vi.clearAllMocks();
});

describe('WelcomeBackView', () => {
	it('passes straight through to the game when there is no reward window', async () => {
		const view = makeView(progress({ hasProgress: false, autoChallengeBoss: true }));

		await view.run();

		// The persisted mode is still reconciled (the toggle is restored on any return)...
		expect(deps.reconcileMode).toHaveBeenCalledWith(true);
		// ...but with no progress there is no re-sync and no gate — the game starts immediately.
		expect(deps.resyncPlayer).not.toHaveBeenCalled();
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('entered');
		expect(view.summary).toBeNull();
	});

	it('enters the game and skips reconciliation when the offline check fails twice in a row', async () => {
		const view = makeView(null);

		await view.run();

		// One retry (#1999): a transient failure gets a second chance before reconciliation is skipped.
		expect(deps.fetchProgress).toHaveBeenCalledTimes(2);
		expect(deps.reconcileMode).not.toHaveBeenCalled();
		expect(deps.resyncPlayer).not.toHaveBeenCalled();
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('entered');
	});

	it('retries once and reconciles mode when the retry succeeds (#1999)', async () => {
		deps = {
			fetchProgress: vi
				.fn<WelcomeBackDeps['fetchProgress']>()
				.mockResolvedValueOnce(null)
				.mockResolvedValueOnce(progress({ hasProgress: false, autoChallengeBoss: true })),
			resyncPlayer: vi.fn(() => Promise.resolve()),
			reconcileMode: vi.fn(),
			enterGame: vi.fn()
		};
		const view = new WelcomeBackView(deps as unknown as WelcomeBackDeps);

		await view.run();

		expect(deps.fetchProgress).toHaveBeenCalledTimes(2);
		expect(deps.reconcileMode).toHaveBeenCalledWith(true);
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('entered');
	});

	it('does not retry when the first fetch succeeds', async () => {
		const view = makeView(progress({ hasProgress: false }));

		await view.run();

		expect(deps.fetchProgress).toHaveBeenCalledTimes(1);
	});

	it('re-syncs state and shows the summary gate without starting the game when there is progress', async () => {
		const summary = progress({ autoChallengeBoss: true, battlesWon: 42 });
		const view = makeView(summary);

		await view.run();

		expect(deps.reconcileMode).toHaveBeenCalledWith(true);
		expect(deps.resyncPlayer).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('summary');
		// `summary` is held in `$state`, so it is compared by value (the rune deep-proxies the object).
		expect(view.summary).toEqual(summary);
		// The idle loop must not start until the gate is dismissed.
		expect(deps.enterGame).not.toHaveBeenCalled();
	});

	it('starts the game exactly once when the gate is dismissed', async () => {
		const view = makeView(progress());
		await view.run();
		expect(view.phase).toBe('summary');

		view.enter();
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('entered');

		// A second dismissal (e.g. a double click) must not re-start the game.
		view.enter();
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
	});

	// A summary carrying activeBattle (#1595/#1596) means the away window's boundary fell inside a battle
	// still in progress; enterGame must resume it via replay-to-offset (#1597) rather than a fresh spawn.
	it("threads the summary's activeBattle into enterGame on dismissal", async () => {
		const activeBattle = {
			id: 3,
			level: 2,
			seed: 9,
			enemyRating: 100,
			selectedSkills: [0],
			attributes: [],
			elapsedOffsetMs: 30000,
			isBossBattle: false
		};
		const view = makeView(progress({ activeBattle }));
		await view.run();

		view.enter();

		expect(deps.enterGame).toHaveBeenCalledWith(activeBattle);
	});

	it('passes undefined to enterGame when there is no activeBattle to resume', async () => {
		const view = makeView(progress());
		await view.run();

		view.enter();

		expect(deps.enterGame).toHaveBeenCalledWith(undefined);
	});

	// #1807: the game page's onDestroy cancels the gate so a run() continuation that resolves after
	// unmount (e.g. a slow GetOfflineProgress round-trip) can't start the engines behind an already-left
	// screen, and enter() can't be invoked afterwards either.
	describe('cancellation (#1807)', () => {
		it('does not reconcile mode or enter the game when cancelled before fetchProgress resolves', async () => {
			let resolveFetch: (value: IOfflineProgressModel | null) => void = () => {};
			deps = {
				fetchProgress: vi.fn(() => new Promise<IOfflineProgressModel | null>((resolve) => (resolveFetch = resolve))),
				resyncPlayer: vi.fn(() => Promise.resolve()),
				reconcileMode: vi.fn(),
				enterGame: vi.fn()
			};
			const view = new WelcomeBackView(deps as unknown as WelcomeBackDeps);

			const runPromise = view.run();
			view.cancel();
			resolveFetch(progress({ hasProgress: false }));
			await runPromise;

			expect(deps.reconcileMode).not.toHaveBeenCalled();
			expect(deps.enterGame).not.toHaveBeenCalled();
			expect(view.phase).toBe('checking');
		});

		it('does not retry or enter the game when cancelled between the first fetch and its retry (#1999)', async () => {
			let resolveFirstFetch: (value: IOfflineProgressModel | null) => void = () => {};
			const fetchProgress = vi
				.fn<WelcomeBackDeps['fetchProgress']>()
				.mockImplementationOnce(() => new Promise((resolve) => (resolveFirstFetch = resolve)));
			deps = {
				fetchProgress,
				resyncPlayer: vi.fn(() => Promise.resolve()),
				reconcileMode: vi.fn(),
				enterGame: vi.fn()
			};
			const view = new WelcomeBackView(deps as unknown as WelcomeBackDeps);

			const runPromise = view.run();
			view.cancel();
			resolveFirstFetch(null);
			await runPromise;

			expect(fetchProgress).toHaveBeenCalledTimes(1);
			expect(deps.reconcileMode).not.toHaveBeenCalled();
			expect(deps.enterGame).not.toHaveBeenCalled();
			expect(view.phase).toBe('checking');
		});

		it('does not show the summary gate when cancelled during the post-progress resync', async () => {
			let resolveResync: () => void = () => {};
			deps = {
				fetchProgress: vi.fn(() => Promise.resolve(progress())),
				resyncPlayer: vi.fn(() => new Promise<void>((resolve) => (resolveResync = resolve))),
				reconcileMode: vi.fn(),
				enterGame: vi.fn()
			};
			const view = new WelcomeBackView(deps as unknown as WelcomeBackDeps);

			const runPromise = view.run();
			// Flush the microtask queue until run() is parked at the resyncPlayer await (reconcileMode
			// already ran by then), then cancel before letting the resync settle.
			await Promise.resolve();
			await Promise.resolve();
			view.cancel();
			resolveResync();
			await runPromise;

			expect(deps.reconcileMode).toHaveBeenCalledTimes(1);
			expect(view.phase).toBe('checking');
			expect(view.summary).toBeNull();
		});

		it('enter() is a no-op once cancelled, even from the "no reward window" fast path', async () => {
			const view = makeView(progress({ hasProgress: false }));
			view.cancel();

			await view.run();

			expect(deps.enterGame).not.toHaveBeenCalled();
			expect(view.phase).toBe('checking');
		});

		it('enter() is a no-op once cancelled after the summary gate is already showing', async () => {
			const view = makeView(progress());
			await view.run();
			expect(view.phase).toBe('summary');

			view.cancel();
			view.enter();

			expect(deps.enterGame).not.toHaveBeenCalled();
			expect(view.phase).toBe('summary');
		});
	});
});
