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

	it('enters the game and skips reconciliation when the offline check fails', async () => {
		const view = makeView(null);

		await view.run();

		expect(deps.reconcileMode).not.toHaveBeenCalled();
		expect(deps.resyncPlayer).not.toHaveBeenCalled();
		expect(deps.enterGame).toHaveBeenCalledTimes(1);
		expect(view.phase).toBe('entered');
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
});
