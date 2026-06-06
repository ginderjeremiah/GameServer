import { apiSocket, ELogType, IApiSocketResponse, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay } from '$lib/common';
import { staticData } from '$stores';
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

export class EnemyManager {
	public currentEnemy: IEnemyInstance | undefined;
	public started = false;

	private newEnemyPromise: Promise<IApiSocketResponse<'NewEnemy'>> | undefined;
	private battleStageUnhook?: Action;

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

	private async watchBattleStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			logMessage(ELogType.EnemyDefeated, staticData.enemies[this.currentEnemy.id].name + ' was defeated!');

			const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: Date.now() });
			if (!defeatResponse.error && defeatResponse.data.rewards) {
				const rewards = defeatResponse.data.rewards;
				playerManager.grantExp(rewards.expReward);
			} else {
				logMessage(ELogType.Debug, 'There was an error defeating the enemy: ' + defeatResponse.error);
			}

			if (defeatResponse.data.cooldown > 0) {
				await battleEngine.startLoading(defeatResponse.data.cooldown);
			}
		}

		if (stage === BattleStage.Victorious || stage === BattleStage.Defeated || stage === BattleStage.Idle) {
			await this.getNewEnemy();
		}
	}
}
