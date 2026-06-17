import { apiSocket, ELogType, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay, isZoneUnlocked, nextZoneByOrder } from '$lib/common';
import { staticData, statistics, playerChallenges } from '$stores';
import { battleEngine, BattleStage, onBattleStageChanged, playerManager } from '../';
import { logMessage } from '../log';

const newEnemyLoadedHook = createHook<[IEnemyInstance]>();
const notifyNewEnemyLoaded = newEnemyLoadedHook.notify;
export const onNewEnemyLoaded = newEnemyLoadedHook.onNotified;

/**
 * Backoff before re-requesting an enemy when the server returns no enemy and no explicit cooldown
 * — i.e. the request failed (e.g. a transient socket/server error, so `data` is absent). Without it,
 * a persistent failure would spin `getNewEnemy` into a tight request loop.
 */
const NEW_ENEMY_RETRY_DELAY_MS = 1000;

/**
 * How long the Zone-Cleared overlay lingers after a dedicated-boss victory before the boss loop
 * either re-challenges (auto-fight) or hands back to the idle farm loop. Drives the pacing of the
 * "victory moment" the fight screen renders.
 */
const BOSS_VICTORY_OVERLAY_MS = 2600;

/**
 * The fight screen's two mutually-exclusive loops. `idle` farms the zone's random enemies (the
 * dedicated boss is "available" to challenge); `boss` runs a dedicated-boss fight (the idle loop is
 * paused). Per the boss design they never run simultaneously.
 */
export type FightMode = 'idle' | 'boss';

export class EnemyManager {
	public currentEnemy: IEnemyInstance | undefined;
	public started = false;

	/** Which loop is active. `boss` ⇒ the player is engaged in a dedicated-boss fight. */
	public mode: FightMode = 'idle';
	/** Whether a boss victory should immediately re-challenge the boss (farming). */
	public autoFight = false;
	/** Set briefly after a boss victory to drive the fight screen's Zone-Cleared overlay. */
	public bossOutcome: 'victory' | undefined;
	/** Whether the most recent boss victory unlocked the next zone (the gating challenge flipped from
	 *  incomplete to complete). Drives the Zone-Cleared overlay's "Next zone unlocked" line. */
	public bossUnlockedNextZone = false;

	private battleStageUnhook?: Action;
	/** Guards the challenge/retreat transitions so a resolving stage change for the battle being
	 *  swapped out is not mistaken for an outcome of the new one. */
	private transitioning = false;
	/** Re-entrancy guard for getNewEnemy: overlapping stage handlers must not both spawn-and-notify an
	 *  enemy (a double-spawn the backend replay would flag as cheating). */
	private fetchingEnemy = false;

	public start() {
		if (!this.started) {
			this.started = true;
			this.battleStageUnhook = onBattleStageChanged((stage) => this.watchBattleStage(stage));
			this.watchBattleStage(battleEngine.stage);
		}
	}

	public stop() {
		if (this.started) {
			this.started = false;
			this.battleStageUnhook?.();
			this.returnToIdle();
		}
	}

	public async getNewEnemy() {
		// Single re-entrancy guard: overlapping stage handlers (e.g. an idle victory racing an Idle/
		// Defeated change) can both reach here, and each would request-and-notify a new enemy —
		// double-counting a spawn. Because the backend replays what the client reports, a double-spawn
		// has anti-cheat consequences, so the second, re-entrant call drops; the first still spawns one.
		if (this.fetchingEnemy) {
			return;
		}
		this.fetchingEnemy = true;
		try {
			// Retry iteratively rather than via self-recursion: each attempt returns to a flat stack
			// (a sustained outage no longer grows the async chain without bound) and `stop()` cancels
			// the loop by flipping `started`.
			while (this.started) {
				const result = await apiSocket.sendSocketCommand('NewEnemy', {
					newZoneId: playerManager.currentZone
				});
				if (result.data?.enemyInstance) {
					this.currentEnemy = result.data.enemyInstance;
					notifyNewEnemyLoaded(this.currentEnemy);
					return;
				}

				// No enemy this time: either the zone is on cooldown (wait it out) or the request failed
				// (`data` is absent — note the optional chaining; an error response carries no data).
				// Back off in both cases, then retry.
				if (result.error) {
					logMessage(ELogType.Debug, 'There was an error loading a new enemy: ' + result.error);
				}
				await delay(result.data?.cooldown ?? NEW_ENEMY_RETRY_DELAY_MS);
			}
		} finally {
			this.fetchingEnemy = false;
		}
	}

	/**
	 * Start a dedicated-boss fight against the current zone's boss. Switches into the boss loop
	 * (the backend abandons any in-progress idle fight) and engages. A bossless zone or a transient
	 * failure falls back to the idle loop.
	 */
	public async challengeBoss() {
		if (this.transitioning) {
			return;
		}
		this.transitioning = true;
		this.mode = 'boss';
		this.bossOutcome = undefined;
		this.bossUnlockedNextZone = false;
		// Freeze the outgoing battle for the duration of the swap so it can't resolve mid-transition.
		battleEngine.pause();
		try {
			const result = await apiSocket.sendSocketCommand('ChallengeBoss', {
				zoneId: playerManager.currentZone
			});
			if (result.data?.enemyInstance) {
				this.currentEnemy = result.data.enemyInstance;
				notifyNewEnemyLoaded(this.currentEnemy);
			} else {
				if (result.error) {
					logMessage(ELogType.Debug, 'There was an error challenging the boss: ' + result.error);
				}
				this.returnToIdle();
				await this.getNewEnemy();
			}
		} finally {
			this.transitioning = false;
		}
	}

	/** Retreat from an in-progress boss fight back to the normal idle farm loop. */
	public async retreatFromBoss() {
		if (this.mode !== 'boss' || this.transitioning) {
			return;
		}
		this.transitioning = true;
		this.returnToIdle();
		battleEngine.pause();
		try {
			await this.getNewEnemy();
		} finally {
			this.transitioning = false;
		}
	}

	/** Toggle auto-fight: when on, a boss victory immediately re-challenges the boss. */
	public setAutoFight(on: boolean) {
		this.autoFight = on;
	}

	private returnToIdle() {
		this.mode = 'idle';
		this.autoFight = false;
		this.bossOutcome = undefined;
		this.bossUnlockedNextZone = false;
	}

	/** Whether the boss loop is still the active, settled context — false once a stop / retreat / handoff
	 *  has transitioned us away. A boss-victory resolution re-checks this after each await so a transition
	 *  landing mid-resolution abandons it instead of clobbering the new state. */
	private get bossLoopActive(): boolean {
		return this.started && this.mode === 'boss' && !this.transitioning;
	}

	private async watchBattleStage(stage: BattleStage) {
		// While swapping the active battle, ignore the outgoing battle's resolving stage changes.
		if (this.transitioning) {
			return;
		}
		if (this.mode === 'boss') {
			await this.watchBossStage(stage);
		} else {
			await this.watchIdleStage(stage);
		}
	}

	private async watchIdleStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			const cooldown = await this.claimVictory();
			if (cooldown > 0) {
				await battleEngine.startLoading(cooldown);
			}
		}

		// The awaited claim/cooldown above can overlap a boss challenge or retreat that hands the loop
		// off (and resolves the cooldown early via reset); if we've since left idle mode or a transition
		// is mid-flight, don't spawn an idle enemy over the new fight.
		if (this.transitioning || this.mode !== 'idle') {
			return;
		}

		if (stage === BattleStage.Victorious || stage === BattleStage.Defeated || stage === BattleStage.Idle) {
			await this.getNewEnemy();
		}
	}

	private async watchBossStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			await this.resolveBossVictory();
		} else if (stage === BattleStage.Defeated && this.currentEnemy) {
			await this.resolveBossLoss();
		}
	}

	private async resolveBossVictory() {
		// Snapshot the boss's zone up front: it identifies the zone being cleared and the gate this clear
		// may unlock, and must stay fixed even if currentZone shifts (a zone-change / retreat) during the
		// awaits below — otherwise the clear and the "next zone unlocked" check could target the wrong zone.
		const clearedZoneId = playerManager.currentZone;

		await this.claimVictory();
		// A stop / retreat that landed during the victory claim has already transitioned us out of the boss
		// loop; abandon the resolution so we don't resurrect the cleared overlay over the new state.
		if (!this.bossLoopActive) {
			return;
		}

		// A dedicated-boss victory clears its zone; surface the "Cleared" seal immediately while the
		// authoritative per-zone statistic is reconciled on the next statistics load.
		statistics.markZoneCleared(clearedZoneId);

		// Did this clear unlock the next zone? Capture the next zone's locked state now (before the clear
		// is reconciled), then — only if it was actually locked — refresh challenge completion (the backend
		// completes the gating challenge during claimVictory) and check whether it flipped open. Skipping
		// the refresh when nothing could change keeps auto-fight re-farming from spamming the endpoint.
		const completed = (id: number) => playerChallenges.isChallengeCompleted(id);
		const nextZone = nextZoneByOrder(staticData.zones ?? [], clearedZoneId);
		const nextWasLocked = nextZone != null && !isZoneUnlocked(nextZone, completed);
		if (nextWasLocked) {
			await playerChallenges.load(true);
			// Re-guard after the reload for the same reason — a stop / retreat may have landed while the
			// challenge refresh was in flight.
			if (!this.bossLoopActive) {
				return;
			}
		}
		this.bossUnlockedNextZone = nextWasLocked && nextZone != null && isZoneUnlocked(nextZone, completed);

		this.bossOutcome = 'victory';

		await delay(BOSS_VICTORY_OVERLAY_MS);
		// A retreat / stop during the overlay window already transitioned us elsewhere.
		if (!this.bossLoopActive) {
			return;
		}
		this.bossOutcome = undefined;
		if (this.autoFight) {
			await this.challengeBoss();
		} else {
			this.returnToIdle();
			await this.getNewEnemy();
		}
	}

	private async resolveBossLoss() {
		// Record the loss explicitly (turning auto-fight off) and drop back to the boss-available
		// state — the normal idle farm loop — honoring the post-loss cooldown.
		const lostResponse = await apiSocket.sendSocketCommand('BattleLost');
		if (lostResponse.error) {
			logMessage(ELogType.Debug, 'There was an error recording the boss loss: ' + lostResponse.error);
		}
		this.returnToIdle();
		// An error response carries no `data` (e.g. a transient socket failure), so guard the
		// dereference the same way `getNewEnemy` does — otherwise a failed BattleLost would throw
		// before `getNewEnemy` runs and strand the player with no new enemy after a boss loss.
		const cooldown = lostResponse.data?.cooldown ?? 0;
		if (cooldown > 0) {
			await battleEngine.startLoading(cooldown);
		}
		await this.getNewEnemy();
	}

	/**
	 * Reports the current enemy's defeat to the server and grants the earned exp. Shared by the idle
	 * and boss victory paths so the two cannot drift. Returns the post-victory cooldown (ms).
	 */
	private async claimVictory(): Promise<number> {
		// Resolve the enemy's name up front (guarding a missing/retired id), but only log the defeat
		// after a successful DefeatEnemy so a failed command can't show "X was defeated!" with no rewards.
		const enemyId = this.currentEnemy?.id;
		const enemyName = enemyId != null ? staticData.enemies?.[enemyId]?.name : undefined;
		const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: Date.now() });
		if (!defeatResponse.error && defeatResponse.data?.rewards) {
			if (enemyName) {
				logMessage(ELogType.EnemyDefeated, enemyName + ' was defeated!');
			}
			playerManager.grantExp(defeatResponse.data.rewards.expReward);
		} else {
			logMessage(ELogType.Debug, 'There was an error defeating the enemy: ' + defeatResponse.error);
		}
		// Guard `data` for a possible error response (absent `data`), now that this is the shared
		// victory path for both the idle and boss loops.
		return defeatResponse.data?.cooldown ?? 0;
	}
}
