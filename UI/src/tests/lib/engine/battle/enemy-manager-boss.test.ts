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
		startLoading: vi.fn(() => Promise.resolve()),
		pause: vi.fn()
	},
	BattleStage: { Idle: 0, Active: 1, Victorious: 2, Defeated: 3, Loading: 4, Paused: 5 },
	playerManager: { currentZone: 3, grantExp: vi.fn() },
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
	playerManager: h.playerManager
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

const bossInstance: IEnemyInstance = { id: 0, level: 18, seed: 1, selectedSkills: [], attributes: [] };
const normalInstance: IEnemyInstance = { id: 0, level: 9, seed: 2, selectedSkills: [], attributes: [] };

const resp = <T extends 'ChallengeBoss' | 'DefeatEnemy' | 'BattleLost' | 'NewEnemy'>(
	name: T,
	data: unknown
): IApiSocketResponse<T> => ({ id: '1', name, data }) as IApiSocketResponse<T>;

const challengeResponse = resp('ChallengeBoss', { enemyInstance: bossInstance });
const challengeError = { id: '1', name: 'ChallengeBoss', error: 'no boss' } as IApiSocketResponse<'ChallengeBoss'>;
const defeatResponse = resp('DefeatEnemy', {
	cooldown: 0,
	rewards: { expReward: 50, newLevel: 1, newExp: 50, statPointsGained: 0, statPointsUsed: 0 }
});
const lostResponse = resp('BattleLost', { cooldown: 5000 });
const newEnemyResponse = resp('NewEnemy', { enemyInstance: normalInstance });

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
		h.battleEngine.pause.mockClear();
		h.playerManager.grantExp.mockClear();
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
		order,
		levelMin: 1,
		levelMax: 10,
		bossLevel: 1,
		unlockChallengeId
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
		expect(send).toHaveBeenCalledWith('ChallengeBoss', { zoneId: 3 });
		expect(manager.currentEnemy).toEqual(bossInstance);
		expect(loaded).toEqual([bossInstance]);
	});

	it('falls back to the idle loop when the zone has no boss', async () => {
		send.mockImplementation((name: string) =>
			name === 'ChallengeBoss' ? Promise.resolve(challengeError) : Promise.resolve(newEnemyResponse)
		);

		await manager.challengeBoss();

		expect(manager.mode).toBe('idle');
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
		expect(manager.currentEnemy).toEqual(normalInstance);
		expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, 'There was an error challenging the boss: no boss');
	});

	it('on a boss victory: reports the defeat, clears the zone, then returns to idle (auto-fight off)', async () => {
		await manager.challengeBoss();

		await fireStage(h.BattleStage.Victorious);

		expect(send).toHaveBeenCalledWith('DefeatEnemy', expect.objectContaining({ timestamp: expect.any(Number) }));
		expect(h.playerManager.grantExp).toHaveBeenCalledWith(50);
		expect(h.statistics.markZoneCleared).toHaveBeenCalledWith(3);
		// Auto-fight off ⇒ hand back to the idle farm loop.
		expect(manager.mode).toBe('idle');
		expect(manager.bossOutcome).toBeUndefined();
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
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
		expect(send).toHaveBeenCalledWith('ChallengeBoss', { zoneId: 3 });
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

	it('on a boss loss: records it, turns auto-fight off, and returns to the idle loop honoring the cooldown', async () => {
		await manager.challengeBoss();
		manager.setAutoFight(true);

		await fireStage(h.BattleStage.Defeated);

		expect(send).toHaveBeenCalledWith('BattleLost');
		expect(manager.mode).toBe('idle');
		expect(manager.autoFight).toBe(false);
		expect(h.battleEngine.startLoading).toHaveBeenCalledWith(5000);
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
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
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
	});

	it('retreats from a boss fight back to the idle loop', async () => {
		await manager.challengeBoss();
		expect(manager.mode).toBe('boss');

		await manager.retreatFromBoss();

		expect(manager.mode).toBe('idle');
		expect(h.battleEngine.pause).toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
		expect(manager.currentEnemy).toEqual(normalInstance);
	});

	it('ignores retreat when not engaged in a boss fight', async () => {
		await manager.retreatFromBoss();
		expect(send).not.toHaveBeenCalledWith('NewEnemy', expect.anything());
	});

	it('toggles auto-fight', () => {
		expect(manager.autoFight).toBe(false);
		manager.setAutoFight(true);
		expect(manager.autoFight).toBe(true);
		manager.setAutoFight(false);
		expect(manager.autoFight).toBe(false);
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

		expect(send).not.toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
	});

	it('still resolves a normal idle victory (DefeatEnemy + next enemy)', async () => {
		// Load a normal enemy first (idle mode), then win.
		await manager.getNewEnemy();
		send.mockClear();

		await fireStage(h.BattleStage.Victorious);

		expect(send).toHaveBeenCalledWith('DefeatEnemy', expect.objectContaining({ timestamp: expect.any(Number) }));
		expect(h.playerManager.grantExp).toHaveBeenCalledWith(50);
		expect(h.statistics.markZoneCleared).not.toHaveBeenCalled();
		expect(send).toHaveBeenCalledWith('NewEnemy', { newZoneId: 3 });
		expect(manager.mode).toBe('idle');
	});

	it('logs the defeat only after a successful DefeatEnemy', async () => {
		await manager.getNewEnemy();

		await fireStage(h.BattleStage.Victorious);

		// The "X was defeated!" line is logged with the resolved enemy name once rewards are granted.
		expect(logMessage).toHaveBeenCalledWith(ELogType.EnemyDefeated, 'Catacomb Lich was defeated!');
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
		expect(h.playerManager.grantExp).not.toHaveBeenCalled();
	});

	it('survives a missing/retired enemy id on victory (still grants rewards, omits the name log)', async () => {
		// The current enemy's id has no reference record, so its name can't be resolved — the victory
		// must not crash, and rewards still apply; the name-dependent log is simply skipped.
		h.staticData.enemies = [];
		await manager.getNewEnemy();
		vi.mocked(logMessage).mockClear();

		await expect(fireStage(h.BattleStage.Victorious)).resolves.not.toThrow();

		expect(h.playerManager.grantExp).toHaveBeenCalledWith(50);
		expect(logMessage).not.toHaveBeenCalledWith(ELogType.EnemyDefeated, expect.anything());
	});
});
