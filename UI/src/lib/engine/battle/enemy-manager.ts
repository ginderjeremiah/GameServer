import { apiSocket, ELogType, IApiSocketResponse, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay } from '$lib/common';
import { staticData, statistics } from '$stores';
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

	private newEnemyPromise: Promise<IApiSocketResponse<'NewEnemy'>> | undefined;
	private battleStageUnhook?: Action;
	/** Guards the challenge/retreat transitions so a resolving stage change for the battle being
	 *  swapped out is not mistaken for an outcome of the new one. */
	private transitioning = false;

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
		if (!this.newEnemyPromise) {
			this.newEnemyPromise = apiSocket.sendSocketCommand('NewEnemy', {
				newZoneId: playerManager.currentZone
			});
		}

		const result = await this.newEnemyPromise;
		this.newEnemyPromise = undefined;
		if (result.data?.enemyInstance) {
			this.currentEnemy = result.data.enemyInstance;
			notifyNewEnemyLoaded(this.currentEnemy);
		} else {
			// No enemy this time: either the zone is on cooldown (wait it out) or the request failed
			// (`data` is absent — note the optional chaining; the original code dereferenced `data`
			// here and threw on an error response). Back off in both cases, then retry.
			if (result.error) {
				logMessage(ELogType.Debug, 'There was an error loading a new enemy: ' + result.error);
			}
			await delay(result.data?.cooldown ?? NEW_ENEMY_RETRY_DELAY_MS);
			await this.getNewEnemy();
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
		await this.claimVictory();
		// A dedicated-boss victory clears its zone; surface the "Cleared" seal immediately while the
		// authoritative per-zone statistic is reconciled on the next statistics load.
		statistics.markZoneCleared(playerManager.currentZone);
		this.bossOutcome = 'victory';

		await delay(BOSS_VICTORY_OVERLAY_MS);
		// A retreat / stop during the overlay window already transitioned us elsewhere.
		if (!this.started || this.mode !== 'boss') {
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
		const cooldown = lostResponse.data.cooldown;
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
		if (this.currentEnemy) {
			logMessage(ELogType.EnemyDefeated, staticData.enemies[this.currentEnemy.id].name + ' was defeated!');
		}
		const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: Date.now() });
		if (!defeatResponse.error && defeatResponse.data.rewards) {
			playerManager.grantExp(defeatResponse.data.rewards.expReward);
		} else {
			logMessage(ELogType.Debug, 'There was an error defeating the enemy: ' + defeatResponse.error);
		}
		return defeatResponse.data.cooldown;
	}
}
