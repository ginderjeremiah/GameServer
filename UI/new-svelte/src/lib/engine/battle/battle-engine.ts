import { Battler } from '$lib/battle';
import { staticData } from '$stores';
import { ELogType, IEnemyInstance } from '$lib/api';
import { logMessage } from '../log';
import { formatNum, createHook, Action } from '$lib/common';
import { onLogicalUpdate } from '../logical-engine';
import { onRenderUpdate } from '../render-engine';
import { inventoryManager } from '../engine';
import { playerManager } from '../player/player-manager';
import { onNewEnemyLoaded } from './enemy-manager';

export enum BattleStage {
	Idle,
	Active,
	Victorious,
	Defeated,
	Loading,
	Paused
}

const { Idle, Active, Victorious, Defeated, Loading, Paused } = BattleStage;

const battleStageChangedHook = createHook<[BattleStage]>();
const notifyBattleStageChanged = battleStageChangedHook.notify;
export const onBattleStageChanged = battleStageChangedHook.onNotified;

export class BattleEngine {
	public stage = Idle;
	public player: Battler = new Battler();
	public enemy: Battler = new Battler();
	public timeElapsed = 0;
	public loadingTime = 0;
	public running = false;

	private logicalUnhook?: Action;
	private renderUnhook?: Action;
	private enemyLoadedUnhook?: Action;

	public start() {
		if (!this.running) {
			this.running = true;
			this.logicalUnhook = onLogicalUpdate((delta) => this.logicalUpdate(delta));
			this.renderUnhook = onRenderUpdate((_, logicalDelta) => this.renderUpdate(logicalDelta));
			this.enemyLoadedUnhook = onNewEnemyLoaded((enemy) => this.reset(enemy));
			this.player.reset(playerManager, inventoryManager.equipmentStats);
		}
	}

	public stop() {
		if (this.running) {
			this.running = false;
			this.logicalUnhook?.();
			this.renderUnhook?.();
			this.enemyLoadedUnhook?.();
		}
	}

	public pause() {
		this.setBattleStage(Paused);
	}

	public resume = () => {
		if (!this.player.isDead && !this.enemy.isDead) {
			this.setBattleStage(Active);
		} else {
			this.setBattleStage(Idle);
		}
	};

	public reset = (enemyInstance: IEnemyInstance) => {
		const enemyData = staticData.enemies;
		this.timeElapsed = 0;
		this.player.reset(playerManager, inventoryManager.equipmentStats);
		this.enemy.reset({ ...enemyInstance, ...enemyData[enemyInstance.id] });
		this.resume();
	};

	public getOpponent(battler: Battler) {
		return battler.id === this.player.id ? this.enemy : this.player;
	}

	public startLoading(loadingTime: number) {
		this.loadingTime = loadingTime;
		this.setBattleStage(Loading);
		const { promise, resolve } = Promise.withResolvers<void>();
		onRenderUpdate((delta, _, unhook) => {
			this.loadingTime -= delta;
			if (this.loadingTime <= 0) {
				resolve();
				unhook();
			}
		}, false);
		return promise;
	}

	private logicalUpdate(timeDelta: number) {
		if (this.stage === Active) {
			const playerSkillsFired = this.player.advanceCooldowns(timeDelta);
			playerSkillsFired.forEach((skill) => {
				const dmg = skill.calculateDamage();
				const finalDmg = this.enemy.takeDamage(dmg);
				logMessage(ELogType.Damage, `You used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
			});
			if (!this.enemy.isDead) {
				const enemySkillsFired = this.enemy.advanceCooldowns(timeDelta);
				enemySkillsFired.forEach((skill) => {
					const dmg = skill.calculateDamage();
					const finalDmg = this.player.takeDamage(dmg);
					logMessage(ELogType.Damage, `${this.enemy.name} used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
				});
			}
			if (this.enemy.isDead) {
				this.setBattleStage(Victorious);
			} else if (this.player.isDead) {
				this.setBattleStage(Defeated);
				logMessage(ELogType.EnemyDefeated, "You've been defeated!");
			}
		}
		this.timeElapsed += timeDelta;
	}

	private renderUpdate(renderDelta: number) {
		if (this.stage === Active) {
			this.player.updateRenderCooldowns(renderDelta);
			this.enemy.updateRenderCooldowns(renderDelta);
		}
	}

	private setBattleStage(stage: BattleStage) {
		this.stage = stage;
		notifyBattleStageChanged(stage);
	}
}
