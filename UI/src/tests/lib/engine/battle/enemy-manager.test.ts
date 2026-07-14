import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiSocket, ELogType, type IApiSocketResponse, type IEnemyInstance } from '$lib/api';
import { delay } from '$lib/common';
import { logMessage } from '$lib/engine/log';
import { EnemyManager, onNewEnemyLoaded } from '$lib/engine/battle/enemy-manager';
// The mocked engine index (below) is the same module enemy-manager reads playerManager/battleEngine from,
// so these imports resolve to that mock — letting the relocation-sync and Home tests observe the state.
import { playerManager, battleEngine } from '$lib/engine';
import { staticData } from '$stores';

vi.mock('$lib/engine/log', () => ({ logMessage: vi.fn() }));

// EnemyManager pulls battleEngine/BattleStage/onBattleStageChanged/playerManager from the engine
// index ('../'). Stub it so importing the unit doesn't spin up the whole engine — getNewEnemy only
// reads playerManager.currentZone, and entering Home calls battleEngine.rest().
vi.mock('$lib/engine', () => ({
	battleEngine: { stage: 0, timeElapsed: 0, startLoading: vi.fn(), rest: vi.fn() },
	BattleStage: { Idle: 0, Victorious: 1, Defeated: 2 },
	onBattleStageChanged: vi.fn(() => () => {}),
	playerManager: { currentZone: 3 }
}));

// staticData.zones is id-indexed; the Home tests populate it so isHomeZone() can resolve the flag.
vi.mock('$stores', () => ({ staticData: { enemies: [], zones: [] } }));

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
	attributes: [],
	enemyRating: 100,
	isBossBattle: false
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
		battleEngine.timeElapsed = 0;
		vi.mocked(logMessage).mockClear();
		vi.mocked(delay).mockClear();
		vi.mocked(battleEngine.startLoading).mockClear();
		sendSocketCommand = vi.spyOn(apiSocket, 'sendSocketCommand');
		sendSocketCommand.mockReset();
	});

	it("requests an enemy for the player's current zone", async () => {
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy()));

		await manager.getNewEnemy();

		expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
			newZoneId: 3,
			clientBattleMs: undefined,
			forceAbandon: false
		});
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

	// #1647: a sub-5-minute reconnect resumes via this ordinary NewEnemy fetch rather than a welcome-back
	// activeBattle hand-back — if the still-in-progress battle it hands back was actually a boss fight, the
	// mode must follow the authoritative flag instead of staying idle.
	it('adopts boss mode when NewEnemy hands back a still-in-progress boss battle', async () => {
		const bossHandback = { ...makeEnemy(2), elapsedOffsetMs: 12000, isBossBattle: true };
		sendSocketCommand.mockResolvedValue(enemyResponse(bossHandback));

		await manager.getNewEnemy();

		expect(manager.currentEnemy).toEqual(bossHandback);
		expect(manager.mode).toBe('boss');
	});

	it('adopts the server-reported zone when the player was relocated out of an unplayable zone', async () => {
		// The server relocated the player (their zone was retired or emptied of spawnable enemies) and
		// reports the authoritative zone alongside the enemy; the client must follow it.
		playerManager.currentZone = 3;
		sendSocketCommand.mockResolvedValue({
			id: '1',
			name: 'NewEnemy',
			data: { enemyInstance: makeEnemy(1), zoneId: 7 }
		} as IApiSocketResponse<'NewEnemy'>);

		await manager.getNewEnemy();

		expect(playerManager.currentZone).toBe(7);
		expect(manager.currentEnemy).toEqual(makeEnemy(1));

		// Restore the shared mock's zone so later tests in the file see the default.
		playerManager.currentZone = 3;
	});

	it('waits out a cooldown response, then retries until an enemy arrives', async () => {
		sendSocketCommand.mockResolvedValueOnce(cooldownResponse(500)).mockResolvedValueOnce(enemyResponse(makeEnemy(1)));

		await manager.getNewEnemy();

		expect(delay).toHaveBeenCalledWith(500);
		expect(manager.currentEnemy).toEqual(makeEnemy(1));
		expect(logMessage).not.toHaveBeenCalled();
	});

	// #1881: when this call's own abandon resolved an idle loss/draw's outcome, the server anchors the
	// returned battle's start to the just-incurred post-battle cooldown (#1851) rather than now. Presenting
	// the enemy immediately would run the client's battle clock ahead of that anchor, so the client must
	// wait out the bundled cooldown first — mirroring the DefeatEnemy/BattleLost cooldown/prefetch flow.
	it('waits out an abandon-incurred cooldown bundled with the enemy before presenting it', async () => {
		const enemy = makeEnemy(5);
		sendSocketCommand.mockResolvedValue({
			id: '1',
			name: 'NewEnemy',
			data: { enemyInstance: enemy, cooldown: 4500 }
		} as IApiSocketResponse<'NewEnemy'>);
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		await manager.getNewEnemy();

		expect(battleEngine.startLoading).toHaveBeenCalledWith(4500);
		expect(manager.currentEnemy).toEqual(enemy);
		expect(loaded).toEqual([enemy]);
	});

	it('presents a freshly spawned enemy immediately when no cooldown is bundled with it', async () => {
		sendSocketCommand.mockResolvedValue({
			id: '1',
			name: 'NewEnemy',
			data: { enemyInstance: makeEnemy(6), cooldown: 0 }
		} as IApiSocketResponse<'NewEnemy'>);

		await manager.getNewEnemy();

		expect(battleEngine.startLoading).not.toHaveBeenCalled();
		expect(manager.currentEnemy).toEqual(makeEnemy(6));
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

	it('does not apply a successful enemy when stop() lands while parked on the NewEnemy request', async () => {
		// The narrow window the loop condition alone doesn't cover: the fetch is parked on the in-flight
		// NewEnemy command (not the backoff) when stop() lands, and the command then resolves with an
		// enemy. The post-await guard must drop it rather than spawn-and-notify over the stopped manager.
		const enemy = makeEnemy(9);
		let resolveSend!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		sendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveSend = resolve)));
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const fetch = manager.getNewEnemy();
		await flush(); // park the loop on the in-flight NewEnemy request
		manager.stop(); // supersede mid-request (clears started, bumps the generation)
		resolveSend(enemyResponse(enemy));
		await fetch;

		expect(manager.currentEnemy).toBeUndefined();
		expect(loaded).toEqual([]);
	});

	it('does not apply a successful enemy when the fetch is superseded while parked on the NewEnemy request', async () => {
		// Same narrow window, but the supersession is a transition (generation bump) rather than a stop:
		// the manager stays started, so only the generation re-check catches it. The enemy a superseded
		// idle fetch receives must not overwrite the fight the transition moved on to.
		const enemy = makeEnemy(11);
		let resolveSend!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		sendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveSend = resolve)));
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const fetch = manager.getNewEnemy();
		await flush(); // park the loop on the in-flight NewEnemy request
		// Simulate a transition (e.g. challengeBoss) superseding this fetch via interruptFetch: the
		// generation moves on while the manager stays started.
		(manager as unknown as { interruptFetch(): void }).interruptFetch();
		resolveSend(enemyResponse(enemy));
		await fetch;

		expect(manager.started).toBe(true);
		expect(manager.currentEnemy).toBeUndefined();
		expect(loaded).toEqual([]);
	});

	// #1934: two same-generation callers can both queue behind an older, superseded fetch (e.g. entering
	// Home bumps the generation while a NewEnemy is in flight, then leaving Home calls getNewEnemy twice
	// before the superseded fetch tears down). Both must not fall through past the guard and launch the
	// current generation's fetch — the first genuinely restarts it, the second must recognize that as its
	// own re-entrant duplicate and drop.
	it('drops the second of two same-generation callers queued behind a superseded fetch', async () => {
		let resolveOld!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		sendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveOld = resolve)));
		const enemy = makeEnemy(13);
		let resolveNew!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		sendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveNew = resolve)));
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const oldFetch = manager.getNewEnemy(); // starts the old-generation fetch
		await flush(); // park it on the in-flight NewEnemy request

		// Supersede it (e.g. enterHome/challengeBoss bumping the generation) while it's still in flight.
		(manager as unknown as { interruptFetch(): void }).interruptFetch();

		// Two same-generation callers both arrive while the superseded fetch is still tearing down.
		const callerA = manager.getNewEnemy();
		const callerB = manager.getNewEnemy();

		// The superseded fetch's own request now resolves; its generation check makes it abandon without
		// spawning, releasing the guard for the two waiters queued behind it.
		resolveOld(enemyResponse(makeEnemy(0)));
		await flush();

		// Only one NewEnemy request should have gone out for the current generation.
		expect(sendSocketCommand).toHaveBeenCalledTimes(2);
		resolveNew(enemyResponse(enemy));
		await Promise.all([oldFetch, callerA, callerB]);

		expect(sendSocketCommand).toHaveBeenCalledTimes(2);
		expect(loaded).toEqual([enemy]);
		expect(manager.currentEnemy).toEqual(enemy);
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

	// #1883: a fresh battleEngine's timeElapsed is a genuine 0, not "no history" — the loop's first-ever
	// NewEnemy request must omit clientBattleMs rather than send that 0, or the backend reads it as "the
	// client never fought this battle" and silently drops whatever stale battle it may still be holding
	// (e.g. a sub-5-minute reconnect the welcome-back gate didn't resume directly).
	it("omits clientBattleMs on the loop's first NewEnemy request even though timeElapsed is genuinely 0", async () => {
		battleEngine.timeElapsed = 0;
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(1)));

		await manager.getNewEnemy();

		expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
			newZoneId: 3,
			clientBattleMs: undefined,
			forceAbandon: false
		});
	});

	it('reports the real elapsed time on a later fetch once the loop has already made its first request', async () => {
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(1)));
		await manager.getNewEnemy(); // consumes the first-fetch omission

		battleEngine.timeElapsed = 4200;
		sendSocketCommand.mockClear();
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(2)));
		await manager.getNewEnemy(); // e.g. an idle loss/draw reporting genuine elapsed time

		expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
			newZoneId: 3,
			clientBattleMs: 4200,
			forceAbandon: false
		});
	});
});

describe('EnemyManager.start', () => {
	let manager: EnemyManager;
	let sendSocketCommand: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		manager = new EnemyManager();
		battleEngine.timeElapsed = 0;
		vi.mocked(logMessage).mockClear();
		sendSocketCommand = vi.spyOn(apiSocket, 'sendSocketCommand');
		sendSocketCommand.mockReset();
	});

	it('re-kicks the idle loop from the Idle baseline, e.g. returning from the admin screen (#881)', async () => {
		// The mocked battleEngine.stage is the Idle baseline that battleEngine.stop() leaves behind. start()
		// reads it and fetches a fresh enemy rather than leaving the fight frozen on resume.
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(5)));

		manager.start();

		await vi.waitFor(() =>
			expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
				newZoneId: 3,
				clientBattleMs: undefined,
				forceAbandon: false
			})
		);
		expect(manager.currentEnemy).toEqual(makeEnemy(5));
	});

	// A GetOfflineProgress summary's activeBattle (#1595/#1596) is presented directly rather than through a
	// fresh NewEnemy fetch — which would report 0ms fought and abandon the handed-back battle (#1597).
	it('presents a server-handed-back active battle directly, without a NewEnemy round trip', () => {
		const activeBattle = { ...makeEnemy(6), elapsedOffsetMs: 45000 };
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		manager.start(activeBattle);

		expect(sendSocketCommand).not.toHaveBeenCalled();
		expect(manager.currentEnemy).toEqual(activeBattle);
		expect(loaded).toEqual([activeBattle]);
		expect(manager.mode).toBe('idle');
	});

	// #1647: a resumed battle handed back as still-active must route into the boss loop when it actually
	// was a boss fight, or the Zone-Cleared overlay/BattleLost/auto-rechallenge bookkeeping never runs.
	it('routes a resumed boss battle into the boss loop', () => {
		const activeBossBattle = { ...makeEnemy(6), elapsedOffsetMs: 45000, isBossBattle: true };

		manager.start(activeBossBattle);

		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(activeBossBattle);
	});

	// #1883: the first-fetch omission is re-armed per start() (e.g. a character switch), not consumed once
	// for the manager's whole lifetime — a fresh loop always has no history for whatever battle the server
	// may still be holding for the newly-selected character.
	it('re-arms the first-fetch omission on a fresh start() after a stop()', async () => {
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(1)));
		manager.start();
		await vi.waitFor(() => expect(manager.currentEnemy).toEqual(makeEnemy(1)));
		manager.stop();

		sendSocketCommand.mockClear();
		battleEngine.timeElapsed = 9000; // stale from the previous character's fight; must not leak through
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(2)));
		manager.start();

		await vi.waitFor(() =>
			expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
				newZoneId: 3,
				clientBattleMs: undefined,
				forceAbandon: false
			})
		);
	});
});

describe('EnemyManager Home zone', () => {
	// id-indexed reference data: zone 3 is a combat zone, zone 5 is the no-combat Home sanctuary.
	const COMBAT_ZONE = 3;
	const HOME_ZONE = 5;
	let manager: EnemyManager;
	let sendSocketCommand: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		manager = new EnemyManager();
		manager.started = true;
		battleEngine.timeElapsed = 0;
		staticData.zones = [];
		staticData.zones[COMBAT_ZONE] = { isHome: false } as (typeof staticData.zones)[number];
		staticData.zones[HOME_ZONE] = { isHome: true } as (typeof staticData.zones)[number];
		playerManager.currentZone = COMBAT_ZONE;
		vi.mocked(battleEngine.rest).mockClear();
		sendSocketCommand = vi.spyOn(apiSocket, 'sendSocketCommand');
		// Default every command to a harmless resolved value so the real socket transport is never hit
		// (entering Home fires SetAutoChallengeBoss via returnToIdle); individual tests assert the calls.
		sendSocketCommand.mockReset();
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy()));
	});

	it('halts the idle loop while in Home — no NewEnemy is requested and the enemy is cleared', async () => {
		playerManager.currentZone = HOME_ZONE;
		manager.currentEnemy = makeEnemy(9);

		await manager.getNewEnemy();

		expect(sendSocketCommand).not.toHaveBeenCalled();
		expect(manager.currentEnemy).toBeUndefined();
	});

	it('navigateToZone into Home stops the live battle and clears the enemy without fetching', () => {
		manager.currentEnemy = makeEnemy(4);

		manager.navigateToZone(HOME_ZONE);

		expect(playerManager.currentZone).toBe(HOME_ZONE);
		expect(battleEngine.rest).toHaveBeenCalled();
		expect(manager.currentEnemy).toBeUndefined();
		expect(sendSocketCommand).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
	});

	it('navigateToZone out of Home resumes the idle loop in the destination zone', async () => {
		playerManager.currentZone = HOME_ZONE;
		sendSocketCommand.mockResolvedValue(enemyResponse(makeEnemy(2)));

		manager.navigateToZone(COMBAT_ZONE);

		expect(playerManager.currentZone).toBe(COMBAT_ZONE);
		await vi.waitFor(() =>
			expect(sendSocketCommand).toHaveBeenCalledWith('NewEnemy', {
				newZoneId: COMBAT_ZONE,
				clientBattleMs: undefined,
				forceAbandon: false
			})
		);
	});
});
