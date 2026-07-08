import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiSocket, ELogType, type IApiSocketResponse, type IEnemyInstance, type IZone } from '$lib/api';
import { delay } from '$lib/common';
import { logMessage } from '$lib/engine/log';
import { EnemyManager, onNewEnemyLoaded } from '$lib/engine/battle/enemy-manager';

// Share a holder so the mocked onBattleStageChanged can hand the registered stage callback back
// to the test, letting it drive Victorious/Defeated transitions deterministically.
const h = vi.hoisted(() => ({
	holder: { stageCb: undefined as ((stage: number) => unknown) | undefined },
	battleEngine: {
		stage: 1, // Active: start() then does not kick off an initial idle getNewEnemy
		timeElapsed: 8880, // the simulated battle duration claimVictory reports as clientTotalMs
		startLoading: vi.fn(() => Promise.resolve()),
		pause: vi.fn(),
		// The idle loop captures the player's battle-state at a battle's end and compares it after the
		// cooldown to decide whether a server-bundled next battle is still parity-safe to present.
		capturePlayerBattleState: vi.fn(() => ({ token: 'state' })),
		playerBattleStateMatches: vi.fn(() => true)
	},
	BattleStage: { Idle: 0, Active: 1, Victorious: 2, Defeated: 3, Loading: 4, Paused: 5, Drawn: 6 },
	playerManager: { currentZone: 3, applyVictoryRewards: vi.fn() },
	resyncPlayerAndInventory: vi.fn(() => Promise.resolve()),
	staticData: {
		enemies: [{ id: 0, name: 'Catacomb Lich', isBoss: true }],
		zones: undefined as IZone[] | undefined
	},
	statistics: { markZoneCleared: vi.fn() },
	playerChallenges: {
		isChallengeCompleted: vi.fn<(id: number) => boolean>(() => false),
		load: vi.fn(() => Promise.resolve())
	}
}));

vi.mock('$lib/engine/log', () => ({ logMessage: vi.fn() }));
vi.mock('$lib/engine', () => ({
	battleEngine: h.battleEngine,
	BattleStage: h.BattleStage,
	onBattleStageChanged: vi.fn((cb: (stage: number) => unknown) => {
		h.holder.stageCb = cb;
		return () => {};
	}),
	playerManager: h.playerManager,
	resyncPlayerAndInventory: h.resyncPlayerAndInventory
}));
vi.mock('$stores', () => ({
	staticData: h.staticData,
	statistics: h.statistics,
	playerChallenges: h.playerChallenges
}));
vi.mock('$lib/common', async (importOriginal) => ({
	...(await importOriginal<typeof import('$lib/common')>()),
	// Resolve the boss-victory overlay delay immediately.
	delay: vi.fn(() => Promise.resolve())
}));

const bossInstance: IEnemyInstance = {
	id: 0,
	level: 18,
	seed: 1,
	selectedSkills: [],
	attributes: [],
	enemyRating: 100,
	isBossBattle: true
};
const normalInstance: IEnemyInstance = {
	id: 0,
	level: 9,
	seed: 2,
	selectedSkills: [],
	attributes: [],
	enemyRating: 100,
	isBossBattle: false
};
// The next idle enemy the server bundles with a victory / boss-loss response so the client can begin it
// without a separate NewEnemy round-trip (distinct from the fetched normalInstance so the two are told apart).
const preparedInstance: IEnemyInstance = {
	id: 0,
	level: 12,
	seed: 7,
	selectedSkills: [],
	attributes: [],
	enemyRating: 100,
	isBossBattle: false
};

const resp = <T extends 'ChallengeBoss' | 'DefeatEnemy' | 'BattleLost' | 'NewEnemy'>(
	name: T,
	data: unknown
): IApiSocketResponse<T> => ({ id: '1', name, data }) as IApiSocketResponse<T>;

const challengeResponse = resp('ChallengeBoss', { enemyInstance: bossInstance });
const challengeError = { id: '1', name: 'ChallengeBoss', error: 'no boss' } as IApiSocketResponse<'ChallengeBoss'>;
const defeatRewards = { expReward: 50, newLevel: 1, newExp: 50, statPointsGained: 0, statPointsUsed: 0 };
const defeatResponse = resp('DefeatEnemy', {
	cooldown: 0,
	rewards: defeatRewards
});
const lostResponse = resp('BattleLost', { cooldown: 5000 });
const newEnemyResponse = resp('NewEnemy', { enemyInstance: normalInstance });
// Battle-end responses that bundle the prefetched next idle battle (the server-bundled flow, #1092).
const bundledDefeatResponse = (cooldown: number) =>
	resp('DefeatEnemy', {
		cooldown,
		rewards: defeatRewards,
		nextEnemy: preparedInstance,
		nextZoneId: 3
	});
const bundledLostResponse = (cooldown: number) =>
	resp('BattleLost', { cooldown, nextEnemy: preparedInstance, nextZoneId: 3 });

/** Routes each socket command to its scenario response by name. */
const routeByName = () =>
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	vi.fn((name: string): Promise<any> => {
		switch (name) {
			case 'ChallengeBoss':
				return Promise.resolve(challengeResponse);
			case 'DefeatEnemy':
				return Promise.resolve(defeatResponse);
			case 'BattleLost':
				return Promise.resolve(lostResponse);
			default:
				return Promise.resolve(newEnemyResponse);
		}
	});

describe('EnemyManager boss mode', () => {
	let manager: EnemyManager;
	let send: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		manager = new EnemyManager();
		vi.mocked(logMessage).mockClear();
		h.battleEngine.startLoading.mockClear();
		h.battleEngine.startLoading.mockReturnValue(Promise.resolve());
		h.battleEngine.pause.mockClear();
		h.battleEngine.capturePlayerBattleState.mockClear();
		h.battleEngine.playerBattleStateMatches.mockReset();
		h.battleEngine.playerBattleStateMatches.mockReturnValue(true);
		h.playerManager.applyVictoryRewards.mockClear();
		h.resyncPlayerAndInventory.mockClear();
		h.statistics.markZoneCleared.mockClear();
		// Default: no zones authored ⇒ no "next zone" to unlock; the unlock tests opt in.
		h.staticData.zones = undefined;
		// Reset the current zone (a mid-claim zone-change test mutates it).
		h.playerManager.currentZone = 3;
		// Restore the enemy reference record (a missing-id test clears it).
		h.staticData.enemies = [{ id: 0, name: 'Catacomb Lich', isBoss: true }];
		h.playerChallenges.isChallengeCompleted.mockReset();
		h.playerChallenges.isChallengeCompleted.mockReturnValue(false);
		h.playerChallenges.load.mockReset();
		h.playerChallenges.load.mockResolvedValue(undefined);
		vi.mocked(delay).mockReset();
		vi.mocked(delay).mockResolvedValue(undefined);
		send = vi.spyOn(apiSocket, 'sendSocketCommand');
		send.mockReset();
		send.mockImplementation(routeByName());
		// Active stage ⇒ start() registers the stage callback without an initial idle request.
		manager.start();
	});

	const fireStage = async (stage: number) => h.holder.stageCb?.(stage);

	// Drain the microtask queue (a macrotask boundary) so an in-flight victory handler settles up to its
	// next awaited point — used to park it on a gated claim/reload before interleaving a transition.
	const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

	// A DefeatEnemy (claimVictory) response gated on a manual release, so a stop / retreat / zone-change
	// can be interleaved while the victory claim is still in flight.
	const gateDefeat = () => {
		let release!: () => void;
		const gate = new Promise<void>((resolve) => (release = resolve));
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy' ? gate.then(() => defeatResponse) : Promise.resolve(newEnemyResponse)
		);
		return release;
	};

	const zone = (id: number, order: number, unlockChallengeId?: number): IZone => ({
		id,
		name: `Zone ${id}`,
		description: '',
		designerNotes: '',
		order,
		levelMin: 1,
		levelMax: 10,
		bossLevel: 1,
		unlockChallengeId,
		isHome: false
	});

	// bossUnlockedNextZone is reset by returnToIdle/challengeBoss once the (mocked-immediate) overlay
	// delay elapses, so capture it at the overlay moment — the instant `delay` is called, right after
	// resolveBossVictory computes it.
	const captureUnlockAtOverlay = () => {
		const captured = { value: undefined as boolean | undefined };
		vi.mocked(delay).mockImplementation(() => {
			captured.value = manager.bossUnlockedNextZone;
			return Promise.resolve();
		});
		return captured;
	};

	it('challenges the current zone boss and engages', async () => {
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		await manager.challengeBoss();

		expect(manager.mode).toBe('boss');
		expect(h.battleEngine.pause).toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('ChallengeBoss', expect.objectContaining({ zoneId: 3 }));
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(loaded).toEqual([bossInstance]);
	});

	it('falls back to the idle loop when the zone has no boss', async () => {
		send.mockImplementation((name: string) =>
			name === 'ChallengeBoss' ? Promise.resolve(challengeError) : Promise.resolve(newEnemyResponse)
		);

		await manager.challengeBoss();

		expect(manager.mode).toBe('idle');
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, 'There was an error challenging the boss: no boss');
	});

	it('does not let a superseded idle fetch clobber the boss when its NewEnemy resolves mid-challenge', async () => {
		// The exact issue scenario: an idle getNewEnemy is parked on its in-flight NewEnemy when
		// challengeBoss supersedes it (bumping the generation) and loads the boss. When the stale idle
		// NewEnemy then resolves with an enemy, it must not overwrite the boss the supersession moved to.
		let releaseNewEnemy!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		const newEnemyGate = new Promise<IApiSocketResponse<'NewEnemy'>>((resolve) => (releaseNewEnemy = resolve));
		send.mockImplementation((name: string) =>
			name === 'NewEnemy' ? newEnemyGate : Promise.resolve(challengeResponse)
		);
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const idleFetch = manager.getNewEnemy();
		await flush(); // park the idle fetch on the held NewEnemy request

		await manager.challengeBoss(); // supersedes the idle fetch and loads the boss
		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);

		releaseNewEnemy(newEnemyResponse); // the superseded idle fetch resolves with a normal enemy
		await idleFetch;

		// The stale idle enemy was dropped: the boss remains, and only it was ever notified.
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(loaded).toEqual([bossInstance]);
	});

	it('falls back to a fresh idle enemy when a failing ChallengeBoss races an in-flight idle fetch', async () => {
		// The #971 bug: an idle getNewEnemy is mid-flight (fetchingEnemy still true) when a *failing*
		// ChallengeBoss falls back to getNewEnemy. The fallback must not be silently dropped by the
		// re-entrancy guard — it waits for the superseded idle fetch to tear down, then spawns a fresh enemy.
		let releaseIdle!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		const idleGate = new Promise<IApiSocketResponse<'NewEnemy'>>((resolve) => (releaseIdle = resolve));
		let newEnemyCalls = 0;
		send.mockImplementation((name: string) => {
			if (name === 'ChallengeBoss') {
				return Promise.resolve(challengeError);
			}
			if (name === 'NewEnemy') {
				newEnemyCalls++;
				// The first NewEnemy is the in-flight idle fetch (held); the fallback fetch is the second.
				return newEnemyCalls === 1 ? idleGate : Promise.resolve(newEnemyResponse);
			}
			return Promise.resolve(newEnemyResponse);
		});
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const idleFetch = manager.getNewEnemy();
		await flush(); // park the idle fetch on its held NewEnemy request

		// ChallengeBoss fails and falls back to getNewEnemy while the idle fetch is still in flight.
		const challenge = manager.challengeBoss();
		await flush(); // the fallback is now awaiting the superseded idle fetch's teardown

		releaseIdle(newEnemyResponse); // the stale idle fetch resolves and abandons (generation moved on)
		await Promise.all([idleFetch, challenge]);

		// The fallback issued a second NewEnemy and spawned a fresh idle enemy rather than being dropped.
		expect(manager.mode).toBe('idle');
		expect(newEnemyCalls).toBe(2);
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(loaded).toEqual([normalInstance]);
	});

	it('on a boss victory: reports the defeat, clears the zone, then returns to idle (auto-fight off)', async () => {
		await manager.challengeBoss();

		await fireStage(h.BattleStage.Victorious);

		expect(send).toHaveBeenCalledWith('DefeatEnemy', expect.objectContaining({ clientTotalMs: expect.any(Number) }));
		expect(h.playerManager.applyVictoryRewards).toHaveBeenCalledWith(defeatRewards);
		expect(h.statistics.markZoneCleared).toHaveBeenCalledWith(3);
		// Auto-fight off ⇒ hand back to the idle farm loop.
		expect(manager.mode).toBe('idle');
		expect(manager.bossOutcome).toBeUndefined();
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
	});

	it('on a boss victory with auto-fight on: re-challenges the boss', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);

		await fireStage(h.BattleStage.Victorious);

		expect(h.statistics.markZoneCleared).toHaveBeenCalledWith(3);
		// Re-engaged: still in boss mode, fresh boss loaded, no idle request.
		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
		expect(send).toHaveBeenCalledWith('ChallengeBoss', expect.objectContaining({ zoneId: 3 }));
	});

	it('on a boss victory that completes the next zone gate: refetches and flags the unlock', async () => {
		// Cleared zone 3; zone 4 (next by order) is gated behind challenge 7. The gate is incomplete
		// until the post-victory refetch flips it (mirroring the backend completing it on the clear).
		h.staticData.zones = [zone(3, 0), zone(4, 1, 7)];
		let gateComplete = false;
		h.playerChallenges.isChallengeCompleted.mockImplementation((id: number) => id === 7 && gateComplete);
		h.playerChallenges.load.mockImplementation(() => {
			gateComplete = true;
			return Promise.resolve();
		});
		const unlock = captureUnlockAtOverlay();

		await manager.challengeBoss();
		await fireStage(h.BattleStage.Victorious);

		// The next zone was locked and this clear flipped its gate ⇒ the refetch ran and the unlock fired.
		expect(h.playerChallenges.load).toHaveBeenCalledWith(true);
		expect(unlock.value).toBe(true);
	});

	it('on a boss victory where the next zone is ungated: no refetch and no unlock flag', async () => {
		// Zone 4 has no gate, so it was already open — nothing to unlock and no reason to refetch.
		h.staticData.zones = [zone(3, 0), zone(4, 1)];
		const unlock = captureUnlockAtOverlay();

		await manager.challengeBoss();
		await fireStage(h.BattleStage.Victorious);

		expect(h.playerChallenges.load).not.toHaveBeenCalled();
		expect(unlock.value).toBe(false);
	});

	it('on a boss victory where the next zone gate is already complete: no refetch and no unlock flag', async () => {
		// Re-farming a zone whose gate was already satisfied must not refetch or re-flag the unlock.
		h.staticData.zones = [zone(3, 0), zone(4, 1, 7)];
		h.playerChallenges.isChallengeCompleted.mockReturnValue(true);
		const unlock = captureUnlockAtOverlay();

		await manager.challengeBoss();
		await fireStage(h.BattleStage.Victorious);

		expect(h.playerChallenges.load).not.toHaveBeenCalled();
		expect(unlock.value).toBe(false);
	});

	it('abandons the boss victory when stop() lands during the victory claim', async () => {
		await manager.challengeBoss();
		const releaseDefeat = gateDefeat();

		const handled = fireStage(h.BattleStage.Victorious);
		await flush(); // park the handler on the awaited claim

		manager.stop();
		releaseDefeat();
		await handled;

		// The resolution bailed right after the claim: no zone clear, no overlay, no re-challenge.
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
		expect(manager.bossOutcome).toBeUndefined();
		expect(manager.bossUnlockedNextZone).toBe(false);
		expect(manager.mode).toBe('idle');
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
	});

	it('abandons the boss victory when a retreat lands during the victory claim', async () => {
		await manager.challengeBoss();
		const releaseDefeat = gateDefeat();

		const handled = fireStage(h.BattleStage.Victorious);
		await flush();

		const retreated = manager.retreatFromBoss();
		releaseDefeat();
		await Promise.all([handled, retreated]);

		// The retreat won: idle loop with a fresh normal enemy, and the victory resolution bailed.
		expect(manager.mode).toBe('idle');
		expect(manager.bossOutcome).toBeUndefined();
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it("clears and unlocks the boss's own zone even if currentZone changes during the victory claim", async () => {
		// Cleared zone 3; zone 4 (next by order) is gated behind challenge 7 and flips open on the refetch.
		h.staticData.zones = [zone(3, 0), zone(4, 1, 7)];
		let gateComplete = false;
		h.playerChallenges.isChallengeCompleted.mockImplementation((id: number) => id === 7 && gateComplete);
		h.playerChallenges.load.mockImplementation(() => {
			gateComplete = true;
			return Promise.resolve();
		});
		const unlock = captureUnlockAtOverlay();

		await manager.challengeBoss();
		const releaseDefeat = gateDefeat();

		const handled = fireStage(h.BattleStage.Victorious);
		await flush();

		// Navigate to a different zone while the claim is still resolving.
		h.playerManager.currentZone = 4;
		releaseDefeat();
		await handled;

		// The clear and unlock target zone 3 (the boss that was fought), not the zone navigated to.
		expect(h.statistics.markZoneCleared).toHaveBeenCalledWith(3);
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalledWith(4);
		expect(unlock.value).toBe(true);
	});

	it('abandons the unlock and overlay when a retreat lands during the challenge reload', async () => {
		// Zone 4 is gated behind challenge 7 (incomplete), so the victory triggers the challenge reload.
		h.staticData.zones = [zone(3, 0), zone(4, 1, 7)];
		h.playerChallenges.isChallengeCompleted.mockReturnValue(false);
		let releaseLoad!: () => void;
		const loadGate = new Promise<void>((resolve) => (releaseLoad = resolve));
		h.playerChallenges.load.mockImplementation(() => loadGate);

		await manager.challengeBoss();

		const handled = fireStage(h.BattleStage.Victorious);
		await flush(); // the claim settles; the handler parks on the gated challenge reload

		const retreated = manager.retreatFromBoss();
		releaseLoad();
		await Promise.all([handled, retreated]);

		// The zone was still marked cleared (the boss died), but the retreat abandoned the unlock + overlay.
		expect(h.statistics.markZoneCleared).toHaveBeenCalledWith(3);
		expect(manager.bossUnlockedNextZone).toBe(false);
		expect(manager.bossOutcome).toBeUndefined();
		expect(manager.mode).toBe('idle');
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('abandons the auto-fight re-challenge when a retreat lands during the victory overlay', async () => {
		// The third resolveBossVictory await boundary: a retreat that lands while the handler is parked on the
		// Zone-Cleared overlay delay (auto-fight on) must abandon at the overlay's bossLoopActive re-check
		// rather than re-challenging the boss over the new idle fight.
		await manager.challengeBoss();
		manager.setAutoFight(true);
		// Gate the (only) overlay delay so a retreat can interleave while the victory handler is parked on it.
		let releaseOverlay!: () => void;
		vi.mocked(delay).mockReturnValueOnce(new Promise<void>((resolve) => (releaseOverlay = resolve)));

		const handled = fireStage(h.BattleStage.Victorious);
		await flush(); // claim + zone clear settle; the handler parks on the overlay delay
		const challengeCallsBefore = send.mock.calls.filter((c: unknown[]) => c[0] === 'ChallengeBoss').length;

		const retreated = manager.retreatFromBoss();
		releaseOverlay();
		await Promise.all([handled, retreated]);

		// The retreat transitioned to the idle loop; the parked victory resolution bailed instead of issuing
		// another ChallengeBoss, and the idle enemy stands.
		expect(manager.mode).toBe('idle');
		expect(send.mock.calls.filter((c: unknown[]) => c[0] === 'ChallengeBoss').length).toBe(challengeCallsBefore);
		expect(manager.bossOutcome).toBeUndefined();
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('on a boss loss: records it, turns auto-fight off, and returns to the idle loop honoring the cooldown', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);

		await fireStage(h.BattleStage.Defeated);

		expect(send).toHaveBeenCalledWith('BattleLost');
		expect(manager.mode).toBe('idle');
		expect(manager.autoFight).toBe(false);
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(5000);
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
		// A loss is not a zone clear.
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
	});

	it('survives a failed BattleLost response (no data) and still resumes the idle loop', async () => {
		await manager.challengeBoss();
		// A failed socket command returns no `data` — dereferencing the cooldown must not throw and
		// strand the player before getNewEnemy runs.
		send.mockImplementation((name: string) =>
			name === 'BattleLost'
				? Promise.resolve({ id: '1', name: 'BattleLost', error: 'boom' } as IApiSocketResponse<'BattleLost'>)
				: Promise.resolve(newEnemyResponse)
		);

		await fireStage(h.BattleStage.Defeated);

		expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, 'There was an error recording the boss loss: boom');
		expect(manager.mode).toBe('idle');
		// Absent data ⇒ cooldown defaults to 0 ⇒ no loading step, but the idle loop still resumes.
		expect(h.battleEngine.startLoading).not.toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
	});

	it('abandons boss-loss resolution when a retreat lands while BattleLost is in flight', async () => {
		// #1696: a retreat pressed while the loss is still being recorded must win — the resolution's own
		// (stale) BattleLost response must not clobber the idle fight the retreat already moved to.
		await manager.challengeBoss();
		let releaseLost!: (r: IApiSocketResponse<'BattleLost'>) => void;
		const lostGate = new Promise<IApiSocketResponse<'BattleLost'>>((resolve) => (releaseLost = resolve));
		send.mockImplementation((name: string) => (name === 'BattleLost' ? lostGate : Promise.resolve(newEnemyResponse)));

		const handled = fireStage(h.BattleStage.Defeated);
		await flush(); // park the loss resolution on the in-flight BattleLost

		const retreated = manager.retreatFromBoss(); // supersedes: bumps the transition generation
		await flush();
		releaseLost(lostResponse); // the stale BattleLost now resolves
		await Promise.all([handled, retreated]);

		// The retreat's own idle enemy stands; the loss resolution bailed rather than re-fetching over it.
		expect(manager.mode).toBe('idle');
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(send.mock.calls.filter((c: unknown[]) => c[0] === 'NewEnemy').length).toBe(1);
	});

	it('does not spawn the stale prepared idle enemy when a challenge lands during the post-loss cooldown', async () => {
		// #1696's exact failure scenario: BattleLost bundles a prefetched idle enemy and a cooldown; a
		// challenge pressed during that cooldown resolves the parked startLoading early (finishLoading) and
		// takes over the boss loop. The loss resolution's continuation must not then present the stale
		// prepared idle enemy over the fight the challenge started.
		await manager.challengeBoss();
		let releaseCooldown!: () => void;
		const cooldownGate = new Promise<void>((resolve) => (releaseCooldown = resolve));
		h.battleEngine.startLoading.mockReturnValueOnce(cooldownGate);
		send.mockImplementation((name: string) =>
			name === 'BattleLost' ? Promise.resolve(bundledLostResponse(5000)) : Promise.resolve(challengeResponse)
		);

		const handled = fireStage(h.BattleStage.Defeated);
		await flush(); // loss recorded; parked on the gated post-loss cooldown
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(5000);

		// The player re-challenges during the cooldown — this bumps the transition generation and engages
		// the boss, mirroring the engine reset that resolves the parked startLoading early in production.
		const challenge = manager.challengeBoss();
		releaseCooldown();
		await Promise.all([handled, challenge]);

		// The boss fight the challenge started stands; the stale prepared idle enemy was never presented.
		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(manager.currentEnemy).not.toEqual(preparedInstance);
	});

	it('on a boss draw (timeout): retreats to the idle loop with auto-fight off, recording no loss', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);

		await fireStage(h.BattleStage.Drawn);

		// A draw is not a death, so no loss is recorded and the zone is not cleared; the player drops back
		// to the idle farm (boss available) rather than re-spawning the boss, with auto-fight turned off.
		// The unresolved boss battle is recorded as abandoned by the backend when the next enemy starts.
		expect(send).not.toHaveBeenCalledWith('BattleLost');
		expect(send).not.toHaveBeenCalledWith('DefeatEnemy', expect.anything());
		expect(manager.mode).toBe('idle');
		expect(manager.autoFight).toBe(false);
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
	});

	it('retreats from a boss fight back to the idle loop', async () => {
		await manager.challengeBoss();
		expect(manager.mode).toBe('boss');

		await manager.retreatFromBoss();

		expect(manager.mode).toBe('idle');
		expect(h.battleEngine.pause).toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('retreat force-abandons a still-in-progress boss fight instead of asking the backend to hand it back', async () => {
		// #1690: retreat must not resume the same boss fight even if the server would otherwise hand a
		// still-active battle back unchanged (as an ordinary NewEnemy does). The frontend signals the
		// discard via forceAbandon; the backend-side discard itself is pinned by
		// BattleServiceIntegrationTests.StartBattle_ForceAbandonAStillInProgressBossBattle_DiscardsItAndStartsAFreshIdleBattle.
		await manager.challengeBoss();
		expect(manager.mode).toBe('boss');

		await manager.retreatFromBoss();

		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ forceAbandon: true }));
	});

	it('ignores retreat when not engaged in a boss fight', async () => {
		await manager.retreatFromBoss();
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
	});

	it('a retreat supersedes an in-flight challenge (last input wins)', async () => {
		// The player presses Challenge, then changes their mind and Retreats while ChallengeBoss is still
		// in flight. The retreat takes over: the boss the challenge was loading must not clobber the idle
		// fight the retreat moved to, and only the idle enemy is ever notified.
		let releaseChallenge!: (r: IApiSocketResponse<'ChallengeBoss'>) => void;
		const challengeGate = new Promise<IApiSocketResponse<'ChallengeBoss'>>((resolve) => (releaseChallenge = resolve));
		send.mockImplementation((name: string) =>
			name === 'ChallengeBoss' ? challengeGate : Promise.resolve(newEnemyResponse)
		);
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);

		const challenge = manager.challengeBoss();
		await flush(); // park on the in-flight ChallengeBoss (mode already 'boss')
		expect(manager.mode).toBe('boss');

		const retreat = manager.retreatFromBoss(); // a challenge (heading to boss) is in flight → supersede
		await retreat;
		expect(manager.mode).toBe('idle');
		expect(manager.currentEnemy).toEqual(normalInstance);

		releaseChallenge(challengeResponse); // the superseded challenge now resolves
		await challenge;

		// The boss was dropped: the idle enemy stands and only it was ever notified.
		expect(manager.mode).toBe('idle');
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(loaded).toEqual([normalInstance]);
	});

	it('a challenge supersedes an in-flight retreat, cancelling its parked backoff', async () => {
		// In a boss fight the player Retreats; NewEnemy is down so the retreat parks in its (held-open) retry
		// backoff. Pressing Challenge supersedes it: the parked backoff is cut short and the boss reloads
		// promptly rather than after the full retry window.
		await manager.challengeBoss(); // engage the boss first
		expect(manager.mode).toBe('boss');

		let releaseDelay: () => void = () => {};
		vi.mocked(delay).mockReturnValue(new Promise<void>((resolve) => (releaseDelay = resolve)));
		send.mockImplementation((name: string) =>
			name === 'NewEnemy'
				? Promise.resolve({ id: '1', name: 'NewEnemy', error: 'outage' } as IApiSocketResponse<'NewEnemy'>)
				: Promise.resolve(challengeResponse)
		);

		const retreat = manager.retreatFromBoss();
		await flush(); // park the retreat's fallback fetch on its backoff (mode now 'idle')
		expect(manager.mode).toBe('idle');

		const challenge = manager.challengeBoss(); // supersede: cancel the backoff and reload the boss
		await Promise.all([retreat, challenge]);

		// Resolving without the held delay elapsing proves the backoff was short-circuited.
		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);
		releaseDelay(); // a late timer firing is harmless
	});

	it('a second challenge while one is in flight is a no-op (no duplicate ChallengeBoss)', async () => {
		// A second Challenge press targets the same destination as the in-flight one, so it is ignored rather
		// than re-sent — a re-send would make the backend abandon and re-spawn the boss (a phantom abandon).
		let releaseChallenge!: (r: IApiSocketResponse<'ChallengeBoss'>) => void;
		const challengeGate = new Promise<IApiSocketResponse<'ChallengeBoss'>>((resolve) => (releaseChallenge = resolve));
		send.mockImplementation((name: string) =>
			name === 'ChallengeBoss' ? challengeGate : Promise.resolve(newEnemyResponse)
		);
		const challengeCount = () => send.mock.calls.filter((c: unknown[]) => c[0] === 'ChallengeBoss').length;

		const first = manager.challengeBoss();
		await flush(); // park on the in-flight ChallengeBoss

		await manager.challengeBoss(); // same intent → ignored, returns at once
		expect(challengeCount()).toBe(1);

		releaseChallenge(challengeResponse);
		await first;

		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(challengeCount()).toBe(1);
	});

	it('a second retreat while one is in flight is a no-op (the in-flight retreat continues)', async () => {
		// A second Retreat press targets the same destination as the in-flight one, so it is ignored and the
		// in-flight retreat carries on — only a single NewEnemy is requested.
		await manager.challengeBoss();
		expect(manager.mode).toBe('boss');

		let releaseNewEnemy!: (r: IApiSocketResponse<'NewEnemy'>) => void;
		const newEnemyGate = new Promise<IApiSocketResponse<'NewEnemy'>>((resolve) => (releaseNewEnemy = resolve));
		let newEnemyCalls = 0;
		send.mockImplementation((name: string) => {
			if (name === 'NewEnemy') {
				newEnemyCalls++;
				return newEnemyGate;
			}
			return Promise.resolve(challengeResponse);
		});

		const first = manager.retreatFromBoss();
		await flush(); // park the retreat on its in-flight NewEnemy (mode now 'idle')
		expect(manager.mode).toBe('idle');

		await manager.retreatFromBoss(); // same intent → ignored, returns at once
		expect(newEnemyCalls).toBe(1);

		releaseNewEnemy(newEnemyResponse);
		await first;

		expect(manager.mode).toBe('idle');
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(newEnemyCalls).toBe(1);
	});

	it('a second challenge after one fails into the idle fallback retries the boss (supersedes)', async () => {
		// The first ChallengeBoss fails and falls back to the idle loop, which itself parks in a held-open
		// retry backoff under an outage. Pressing Challenge again supersedes that fallback and retries the
		// boss rather than being dropped — a player who keeps pressing Challenge wants the boss.
		let releaseDelay: () => void = () => {};
		vi.mocked(delay).mockReturnValue(new Promise<void>((resolve) => (releaseDelay = resolve)));
		let challengeCalls = 0;
		send.mockImplementation((name: string) => {
			if (name === 'ChallengeBoss') {
				challengeCalls++;
				// The first challenge fails; the retry (second press) succeeds.
				return challengeCalls === 1 ? Promise.resolve(challengeError) : Promise.resolve(challengeResponse);
			}
			// The idle fallback's NewEnemy is down, so the first challenge parks in its backoff.
			return Promise.resolve({ id: '1', name: 'NewEnemy', error: 'outage' } as IApiSocketResponse<'NewEnemy'>);
		});

		const first = manager.challengeBoss();
		await flush(); // first failed → fell back to idle → parked in the held backoff (mode 'idle')
		expect(manager.mode).toBe('idle');

		const second = manager.challengeBoss(); // supersede the fallback and retry the boss
		await Promise.all([first, second]);

		expect(challengeCalls).toBe(2);
		expect(manager.mode).toBe('boss');
		expect(manager.currentEnemy).toEqual(bossInstance);
		releaseDelay();
	});

	it('toggles auto-fight', () => {
		expect(manager.autoFight).toBe(false);
		manager.setAutoFight(true);
		expect(manager.autoFight).toBe(true);
		manager.setAutoFight(false);
		expect(manager.autoFight).toBe(false);
	});

	it('re-arms auto-fight from the persisted boss mode (welcome-back reconciliation)', () => {
		// The live autoFight always starts false; the welcome-back gate (#1043) re-arms it to what the
		// player left, so a returning boss-farmer's toggle is restored (pre-armed intent, not engagement).
		manager.reconcilePersistedMode(true);
		expect(manager.autoFight).toBe(true);
	});

	it('leaves auto-fight off when the persisted mode is idle (welcome-back reconciliation)', () => {
		manager.reconcilePersistedMode(false);
		expect(manager.autoFight).toBe(false);
	});

	it('seeds the sync baseline so a reconciled boss-farmer does not re-emit a redundant boss sync', async () => {
		// reconcilePersistedMode aligns the dedup baseline with the backend's persisted value, so engaging
		// the boss (which mirrors the live mode) recognises boss is already persisted and emits nothing.
		manager.reconcilePersistedMode(true);
		send.mockClear();

		await manager.challengeBoss();

		expect(send).not.toHaveBeenCalledWith('SetAutoChallengeBoss', true);
	});

	it('does not persist boss mode when auto-fight is pre-armed while idle-farming', () => {
		// Option A (#1067): toggling auto-fight on from the boss trigger while still idle-farming is intent,
		// not engagement — it must NOT persist boss, or the offline sim would resume a never-challenged player
		// as a boss-farmer. The live flag flips, but the persisted mode stays idle.
		manager.setAutoFight(true);
		expect(manager.autoFight).toBe(true);
		expect(send).not.toHaveBeenCalledWith('SetAutoChallengeBoss', true);
	});

	it('persists boss mode when auto-fight is toggled on while engaged in the boss loop', async () => {
		// Mirroring the live auto-fight state to the durable player so the offline sim resumes the boss loop.
		// The boss is always the current zone's boss, so only the enabled flag is sent (no zone).
		await manager.challengeBoss();
		send.mockClear();

		manager.setAutoFight(true);

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', true);
	});

	it('persists idle mode when auto-fight is toggled off while engaged in the boss loop', async () => {
		// The one-off-mid-farm handoff: turning auto-fight off during an active boss fight means the next
		// victory hands back to idle, so the persisted mode must flip to idle even though mode is still 'boss'.
		// Directly exercises the mode gate (a fresh-idle toggle-off would read false regardless of the gate).
		await manager.challengeBoss();
		manager.setAutoFight(true);
		send.mockClear();

		manager.setAutoFight(false);

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', false);
	});

	it('persists boss mode when a pre-armed auto-fight player then challenges', async () => {
		// The pre-arm becomes real boss-farming the moment the player actually challenges: challengeBoss
		// re-syncs and persists boss even though the toggle itself (while idle) did not.
		manager.setAutoFight(true);
		expect(send).not.toHaveBeenCalledWith('SetAutoChallengeBoss', true);

		await manager.challengeBoss();

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', true);
	});

	it('persists idle mode to the backend when auto-fight is toggled off', () => {
		manager.setAutoFight(false);
		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', false);
	});

	it('syncs idle mode when retreating from the boss (returnToIdle)', async () => {
		// Boss-farming (persisted boss), then retreat back to idle ⇒ the persisted mode flips to idle.
		await manager.challengeBoss();
		manager.setAutoFight(true);
		send.mockClear();

		await manager.retreatFromBoss();

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', false);
	});

	it('does not re-sync idle mode when the persisted mode is already idle (dedup)', async () => {
		// Every returnToIdle path fires a sync, but with no boss ever persisted the redundant idle syncs are
		// deduped: a retreat from a one-off (auto-fight-off) challenge must not re-emit an idle command.
		await manager.challengeBoss();
		send.mockClear();

		await manager.retreatFromBoss();

		expect(send).not.toHaveBeenCalledWith('SetAutoChallengeBoss', expect.anything());
	});

	it('syncs idle mode on a boss loss (returnToIdle)', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);
		send.mockClear();

		await fireStage(h.BattleStage.Defeated);

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', false);
	});

	it('syncs idle mode on a boss draw (returnToIdle)', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);
		send.mockClear();

		await fireStage(h.BattleStage.Drawn);

		expect(send).toHaveBeenCalledWith('SetAutoChallengeBoss', false);
	});

	it('does not sync the persisted mode on teardown (stop)', () => {
		// stop() routes through returnToIdle(false): teardown is not a user intent change, so it must not
		// clobber a disconnecting boss-farmer's persisted mode. Pins the deliberate no-sync branch so a
		// later "simplification" back to returnToIdle() (which would sync) is caught.
		manager.setAutoFight(true);
		send.mockClear();

		manager.stop();

		expect(send).not.toHaveBeenCalledWith('SetAutoChallengeBoss', expect.anything());
	});

	it('does not spawn an idle enemy when a boss handoff lands during the post-victory cooldown', async () => {
		// An idle victory enters a cooldown; mid-cooldown the player challenges the boss (mode flips to
		// boss, and reset resolves the cooldown early). When the cooldown resolves, the idle handler must
		// not spawn a stray idle enemy over the new boss fight.
		await manager.getNewEnemy(); // idle enemy loaded; mode stays idle
		let releaseCooldown!: () => void;
		const cooldownGate = new Promise<void>((resolve) => (releaseCooldown = resolve));
		h.battleEngine.startLoading.mockReturnValueOnce(cooldownGate);
		// The idle victory must report a cooldown so the handler waits on startLoading.
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy'
				? Promise.resolve(
						resp('DefeatEnemy', {
							cooldown: 3000,
							rewards: { expReward: 50, newLevel: 1, newExp: 50, statPointsGained: 0, statPointsUsed: 0 }
						})
					)
				: Promise.resolve(newEnemyResponse)
		);

		const handled = fireStage(h.BattleStage.Victorious);
		// Let claimVictory settle so the handler is parked on the awaited startLoading.
		await new Promise((resolve) => setTimeout(resolve, 0));
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(3000);

		// Simulate the boss handoff during the cooldown, then release it.
		manager.mode = 'boss';
		send.mockClear();
		releaseCooldown();
		await handled;

		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
	});

	it('still resolves a normal idle victory (DefeatEnemy + next enemy)', async () => {
		// Load a normal enemy first (idle mode), then win.
		await manager.getNewEnemy();
		send.mockClear();

		await fireStage(h.BattleStage.Victorious);

		expect(send).toHaveBeenCalledWith('DefeatEnemy', expect.objectContaining({ clientTotalMs: expect.any(Number) }));
		expect(h.playerManager.applyVictoryRewards).toHaveBeenCalledWith(defeatRewards);
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', expect.objectContaining({ newZoneId: 3 }));
		expect(manager.mode).toBe('idle');
	});

	it('on an idle draw (timeout): fetches the next enemy without claiming a victory or recording a loss', async () => {
		// A 2-minute stalemate ends as a draw. The idle farm simply continues — no DefeatEnemy (no rewards)
		// and no BattleLost (a draw is not a death); the unresolved battle is recorded as abandoned by the
		// backend when the next enemy starts.
		await manager.getNewEnemy();
		send.mockClear();

		await fireStage(h.BattleStage.Drawn);

		expect(send).not.toHaveBeenCalledWith('DefeatEnemy', expect.anything());
		expect(send).not.toHaveBeenCalledWith('BattleLost');
		// The drawn battle was fought to the cap, so the fetch reports the elapsed time the client simulated
		// (battleEngine.timeElapsed) — the backend abandon re-simulates that window and records the draw.
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3, clientBattleMs: 8880, forceAbandon: false });
		expect(manager.mode).toBe('idle');
	});

	it('logs the defeat only after a successful DefeatEnemy', async () => {
		await manager.getNewEnemy();

		await fireStage(h.BattleStage.Victorious);

		// The "X was defeated!" line is logged with the resolved enemy name once rewards are granted.
		expect(logMessage).toHaveBeenCalledWith(ELogType.EnemyDefeated, 'Catacomb Lich was defeated!');
		// A successful claim needs no resync — the response itself carried the authoritative state.
		expect(h.resyncPlayerAndInventory).not.toHaveBeenCalled();
	});

	it('does not log the defeat when DefeatEnemy fails (no premature "defeated" line)', async () => {
		await manager.getNewEnemy();
		// Fail the defeat command: no rewards ⇒ the player must not see "X was defeated!".
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy'
				? Promise.resolve({ id: '1', name: 'DefeatEnemy', error: 'boom' } as IApiSocketResponse<'DefeatEnemy'>)
				: Promise.resolve(newEnemyResponse)
		);
		vi.mocked(logMessage).mockClear();

		await fireStage(h.BattleStage.Victorious);

		expect(logMessage).not.toHaveBeenCalledWith(ELogType.EnemyDefeated, expect.anything());
		expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, 'There was an error defeating the enemy: boom');
		expect(h.playerManager.applyVictoryRewards).not.toHaveBeenCalled();
		// The transport can settle a lost/timed-out DefeatEnemy as an error even when it actually succeeded
		// server-side, so an errored claim resyncs the authoritative player state (and the inventory derived
		// from it, in case the outage-window victory unlocked an item/mod) rather than leaving the client's
		// exp/level silently diverged.
		expect(h.resyncPlayerAndInventory).toHaveBeenCalledOnce();
	});

	it('survives a missing/retired enemy id on victory (still grants rewards, omits the name log)', async () => {
		// The current enemy's id has no reference record, so its name can't be resolved — the victory
		// must not crash, and rewards still apply; the name-dependent log is simply skipped.
		h.staticData.enemies = [];
		await manager.getNewEnemy();
		vi.mocked(logMessage).mockClear();

		await expect(fireStage(h.BattleStage.Victorious)).resolves.not.toThrow();

		expect(h.playerManager.applyVictoryRewards).toHaveBeenCalledWith(defeatRewards);
		expect(logMessage).not.toHaveBeenCalledWith(ELogType.EnemyDefeated, expect.anything());
	});

	// --- Server-bundled next battle (#1092): hide the NewEnemy fetch latency under the cooldown ---

	it('presents the server-bundled next idle enemy after a victory, with no NewEnemy round-trip', async () => {
		await manager.getNewEnemy(); // load an idle enemy first (mode idle)
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy' ? Promise.resolve(bundledDefeatResponse(3000)) : Promise.resolve(newEnemyResponse)
		);
		send.mockClear();

		await fireStage(h.BattleStage.Victorious);

		// The cooldown is still served, but the next enemy was bundled — so no separate NewEnemy request.
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(3000);
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
		expect(manager.currentEnemy).toEqual(preparedInstance);
		expect(loaded).toEqual([preparedInstance]);
	});

	it('falls back to a fresh fetch when the player changes zone during the post-victory cooldown', async () => {
		await manager.getNewEnemy();
		let releaseCooldown!: () => void;
		const cooldownGate = new Promise<void>((resolve) => (releaseCooldown = resolve));
		h.battleEngine.startLoading.mockReturnValueOnce(cooldownGate);
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy' ? Promise.resolve(bundledDefeatResponse(3000)) : Promise.resolve(newEnemyResponse)
		);

		const handled = fireStage(h.BattleStage.Victorious);
		await flush(); // parked on the gated cooldown
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(3000);

		// Navigate to a different zone during the cooldown, then let it elapse.
		h.playerManager.currentZone = 7;
		send.mockClear();
		releaseCooldown();
		await handled;

		// The bundled enemy was for zone 3; the player is now in zone 7, so it is discarded and a fresh enemy
		// is fetched for (and the server told about) the new zone. The discarded prefetch was never fought, so
		// the fetch reports clientBattleMs 0 — the backend records no phantom abandon for it.
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 7, clientBattleMs: 0, forceAbandon: false });
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('falls back to a fresh fetch when the player changes their build during the post-victory cooldown', async () => {
		await manager.getNewEnemy();
		let releaseCooldown!: () => void;
		const cooldownGate = new Promise<void>((resolve) => (releaseCooldown = resolve));
		h.battleEngine.startLoading.mockReturnValueOnce(cooldownGate);
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy' ? Promise.resolve(bundledDefeatResponse(3000)) : Promise.resolve(newEnemyResponse)
		);

		const handled = fireStage(h.BattleStage.Victorious);
		await flush();

		// A gear/stat/loadout change during the cooldown makes the prefetch's frozen server snapshot diverge
		// from what the frontend would now derive — the loop must re-fetch for a fresh, matching snapshot.
		h.battleEngine.playerBattleStateMatches.mockReturnValue(false);
		send.mockClear();
		releaseCooldown();
		await handled;

		// The never-fought prefetch reports clientBattleMs 0, so the backend records no phantom abandon for it.
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3, clientBattleMs: 0, forceAbandon: false });
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('presents the server-bundled next idle enemy after a boss loss, with no NewEnemy round-trip', async () => {
		await manager.challengeBoss();
		const loaded: IEnemyInstance[] = [];
		onNewEnemyLoaded((e) => loaded.push(e), false);
		send.mockImplementation((name: string) =>
			name === 'BattleLost' ? Promise.resolve(bundledLostResponse(5000)) : Promise.resolve(newEnemyResponse)
		);
		send.mockClear();

		await fireStage(h.BattleStage.Defeated);

		expect(send).toHaveBeenCalledWith('BattleLost');
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(5000);
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
		expect(manager.currentEnemy).toEqual(preparedInstance);
		expect(loaded).toEqual([preparedInstance]);
		expect(manager.mode).toBe('idle');
	});

	it('does not spawn the bundled idle enemy when a boss challenge lands during the post-victory cooldown', async () => {
		// The bundled-present path bypasses getNewEnemy's fetch-generation guard, so it must re-check the
		// transition guard itself: a boss challenge during the cooldown owns the next fight.
		await manager.getNewEnemy();
		let releaseCooldown!: () => void;
		const cooldownGate = new Promise<void>((resolve) => (releaseCooldown = resolve));
		h.battleEngine.startLoading.mockReturnValueOnce(cooldownGate);
		send.mockImplementation((name: string) =>
			name === 'DefeatEnemy' ? Promise.resolve(bundledDefeatResponse(3000)) : Promise.resolve(newEnemyResponse)
		);

		const handled = fireStage(h.BattleStage.Victorious);
		await flush(); // parked on the gated cooldown

		// Simulate a boss handoff during the cooldown, then release it.
		manager.mode = 'boss';
		releaseCooldown();
		await handled;

		// The bundled idle enemy was not presented over the boss fight.
		expect(manager.currentEnemy).not.toEqual(preparedInstance);
	});
});
