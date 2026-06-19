import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiSocket, ELogType, type IApiSocketResponse, type IEnemyInstance } from '$lib/api';
import { delay } from '$lib/common';
import { logMessage } from '$lib/engine/log';
import { EnemyManager, onNewEnemyLoaded } from '$lib/engine/battle/enemy-manager';

vi.mock('$lib/engine/log', () => ({ logMessage: vi.fn() }));

// EnemyManager pulls battleEngine/BattleStage/onBattleStageChanged/playerManager from the engine
// index ('../'). Stub it so importing the unit doesn't spin up the whole engine — getNewEnemy only
// reads playerManager.currentZone.
vi.mock('$lib/engine', () => ({
	battleEngine: { stage: 0, startLoading: vi.fn() },
	BattleStage: { Idle: 0, Victorious: 1, Defeated: 2 },
	onBattleStageChanged: vi.fn(() => () => {}),
	playerManager: { currentZone: 3 }
}));

vi.mock('$stores', () => ({ staticData: { enemies: [] } }));

// Keep the real hook plumbing (createHook); only replace delay so retries resolve immediately
// instead of waiting on real timers, while still letting us assert how long it backed off for.
vi.mock('$lib/common', async (importOriginal) => ({
	...(await importOriginal<typeof import('$lib/common')>()),
	delay: vi.fn(() => Promise.resolve())
}));

const makeEnemy = (id = 0): IEnemyInstance => ({
	id,
	level: 1,
	seed: 123,
	selectedSkills: [0],
	attributes: []
});

const enemyResponse = (enemy: IEnemyInstance): IApiSocketResponse<'NewEnemy'> => ({
	id: '1',
	name: 'NewEnemy',
	data: { enemyInstance: enemy }
});

const cooldownResponse = (cooldown: number): IApiSocketResponse<'NewEnemy'> => ({
	id: '1',
	name: 'NewEnemy',
	data: { cooldown }
});

// An error response carries no data — the shape a failed socket command actually returns. The
// generated type optimistically marks `data` as always-present, so cast past it deliberately.
const errorResponse = (error: string): IApiSocketResponse<'NewEnemy'> =>
	({ id: '1', name: 'NewEnemy', error }) as IApiSocketResponse<'NewEnemy'>;

/** Drains the microtask queue (a macrotask boundary) so an in-flight fetch settles up to its next
 *  awaited point — used to park it on a retry backoff before interleaving a stop. */
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('EnemyManager.getNewEnemy', () => {
	let manager: EnemyManager;
	let sendSocketCommand: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		manager = new EnemyManager();
		// The retry loop runs while the manager is started; flip it on directly (start() would also
		// hook battle-stage changes, which these focused unit tests don't exercise).
		manager.started = true;
		vi.mocked(logMessage).mockClear();
		vi.mocked(delay).mockClear();
		sendSocketCommand = vi.spyOn(apiSocket, 'sendSocketCommand');
		sendSocketCommand.mockReset();
	});

	it("requests an enemy for the player's current zone", async () => {
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy()));

		await manager.getNewEnemy();

		expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
	});

	it('stores the enemy and notifies listeners on success', async () => {
		const enemy = makeEnemy(2);
		sendSocketCommand.mockResolvedValue(enemyResponse(enemy));
		const loaded: IEnemyInstance[] = [];
		// cleanupOnDestroy = false: there's no component lifecycle in a unit test, so skip the
		// Svelte onDestroy registration the hook would otherwise make.
		onNewEnemyLoaded((e) => loaded.push(e), false);

		await manager.getNewEnemy();

		expect(manager.currentEnemy).toEqual(enemy);
		expect(loaded).toEqual([enemy]);
		expect(delay).not.toHaveBeenCalled();
		expect(logMessage).not.toHaveBeenCalled();
	});

	it('waits out a cooldown response, then retries until an enemy arrives', async () => {
		sendSocketCommand.mockResolvedValueOnce(cooldownResponse(500)).mockResolvedValueOnce(enemyResponse(makeEnemy(1)));

		await manager.getNewEnemy();

		expect(delay).toHaveBeenCalledWith(500);
		expect(manager.currentEnemy).toEqual(makeEnemy(1));
		expect(logMessage).not.toHaveBeenCalled();
	});

	it('backs off by the default retry delay on a no-enemy response with cooldown 0 (no tight loop)', async () => {
		// A no-enemy response carrying `cooldown: 0` must still wait out the default retry delay rather
		// than spinning the loop with zero backoff; the explicit retry delay applies whenever there is no enemy.
		sendSocketCommand.mockResolvedValueOnce(cooldownResponse(0)).mockResolvedValueOnce(enemyResponse(makeEnemy(8)));

		await manager.getNewEnemy();

		expect(delay).toHaveBeenCalledWith(1000);
		expect(delay).not.toHaveBeenCalledWith(0);
		expect(manager.currentEnemy).toEqual(makeEnemy(8));
	});

	it('does not throw on an error response, but logs it and retries after a backoff', async () => {
		sendSocketCommand.mockResolvedValueOnce(errorResponse('boom')).mockResolvedValueOnce(enemyResponse(makeEnemy(4)));

		await expect(manager.getNewEnemy()).resolves.toBeUndefined();

		expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, 'There was an error loading a new enemy: boom');
		// Backs off by the default retry delay (no explicit cooldown was provided).
		expect(delay).toHaveBeenCalledWith(1000);
		expect(manager.currentEnemy).toEqual(makeEnemy(4));
	});

	it('does not request an enemy when the manager is not started', async () => {
		manager.started = false;

		await manager.getNewEnemy();

		expect(sendSocketCommand).not.toHaveBeenCalled();
		expect(manager.currentEnemy).toBeUndefined();
	});

	it('stops retrying once the manager is stopped mid-backoff (a sustained outage is bounded)', async () => {
		sendSocketCommand.mockResolvedValue(errorResponse('outage'));
		// Simulate stop() landing during the retry backoff: the loop must not request again.
		vi.mocked(delay).mockImplementationOnce(() => {
			manager.started = false;
			return Promise.resolve();
		});

		await manager.getNewEnemy();

		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(manager.currentEnemy).toBeUndefined();
	});

	it('short-circuits an in-flight retry backoff on stop() rather than waiting out the delay', async () => {
		// Park the loop in its backoff: the first request fails, so it awaits the delay — hold that delay
		// open so it can only be released by the cancellation path, not by the timer elapsing.
		sendSocketCommand.mockResolvedValue(errorResponse('outage'));
		let releaseDelay: () => void = () => {};
		vi.mocked(delay).mockReturnValue(new Promise<void>((resolve) => (releaseDelay = resolve)));

		const fetch = manager.getNewEnemy();
		await flush(); // let the first request resolve and the loop park on the backoff
		expect(sendSocketCommand).toHaveBeenCalledTimes(1);

		// stop() cancels the backoff and clears `started`; the loop wakes and exits without a further
		// request, even though the held delay never elapsed.
		manager.stop();
		await fetch;

		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(manager.currentEnemy).toBeUndefined();
		releaseDelay(); // the real timer firing late is harmless (the backoff already settled)
	});

	it('drops a concurrent re-entrant call so a stage-change race spawns a single enemy', async () => {
		// Two overlapping stage handlers (e.g. an idle victory racing an Idle change) can both reach
		// getNewEnemy; each would request-and-notify an enemy, double-counting the spawn. Since the
		// backend replays what the client reports, that is an anti-cheat hazard — the second call drops.
		const enemy = makeEnemy(7);
		let resolveSend!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		sendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveSend = resolve)));
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const first = manager.getNewEnemy();
		const second = manager.getNewEnemy(); // re-entrant while the first request is still in flight
		resolveSend(enemyResponse(enemy));
		await Promise.all([first, second]);

		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(loaded).toEqual([enemy]);
		expect(manager.currentEnemy).toEqual(enemy);
	});
});

describe('EnemyManager.start', () => {
	let manager: EnemyManager;
	let sendSocketCommand: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		manager = new EnemyManager();
		vi.mocked(logMessage).mockClear();
		sendSocketCommand = vi.spyOn(apiSocket, 'sendSocketCommand');
		sendSocketCommand.mockReset();
	});

	it('re-kicks the idle loop from the Idle baseline, e.g. returning from the admin screen (#881)', async () => {
		// The mocked battleEngine.stage is the Idle baseline that battleEngine.stop() leaves behind. start()
		// reads it and fetches a fresh enemy rather than leaving the fight frozen on resume.
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(5)));

		manager.start();

		await vi.waitFor(() => expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 }));
		expect(manager.currentEnemy).toEqual(makeEnemy(5));
	});
});
