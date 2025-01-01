import { apiSocket, ELogSetting, IApiSocketResponse, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay } from '$lib/common';
import { staticData } from '$stores';
import {
	battleEngine,
	BattleStage,
	inventoryManager,
	onBattleStageChanged,
	playerManager
} from '../';
import { logMessage } from '../log';

const newEnemyLoadedHook = createHook<[IEnemyInstance]>();
const notifyNewEnemyLoaded = newEnemyLoadedHook.notify;
export const onNewEnemyLoaded = newEnemyLoadedHook.onNotified;

export class EnemyManager {
	public currentEnemy: IEnemyInstance | undefined;
	public started = false;

	private newEnemyPromise: Promise<IApiSocketResponse<'NewEnemy'>> | undefined;
	private battleStageUnhook?: Action;

	public start() {
		if (!this.started) {
			this.started = true;
			const that = this;
			this.battleStageUnhook = onBattleStageChanged((stage) => that.watchBattleStage(stage));
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
			if (result.data.cooldown) {
				await delay(result.data.cooldown);
			}
			await this.getNewEnemy();
		}
	}

	private async watchBattleStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			logMessage(
				ELogSetting.EnemyDefeated,
				staticData.enemies[this.currentEnemy.id].name + ' was defeated!'
			);

			const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', this.currentEnemy);
			if (!defeatResponse.error && defeatResponse.data.rewards) {
				const rewards = defeatResponse.data.rewards;
				playerManager.grantExp(rewards.expReward);
				inventoryManager.addInventoryItems(rewards.drops);
			} else {
				logMessage(
					ELogSetting.Debug,
					'There was an error defeating the enemy: ' + defeatResponse.error
				);
			}

			if (defeatResponse.data.cooldown > 0) {
				await battleEngine.startLoading(defeatResponse.data.cooldown);
			}
		}

		if (
			stage === BattleStage.Victorious ||
			stage === BattleStage.Defeated ||
			stage === BattleStage.Idle
		) {
			await this.getNewEnemy();
		}
	}
}
