import { Battler, battleStep, type BattleStepLog } from '$lib/battle';
import { staticData } from '$stores';
import { ELogType, IEnemyInstance } from '$lib/api';
import { logMessage } from '../log';
import { formatNum, createHook, Action, effectLogMessage, attributeEnumName } from '$lib/common';
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

	/** Reused per-tick sink the shared battleStep populates with this tick's newly-applied effects and
	 *  DoT/HoT amounts, so the engine can turn them into combat-log lines (frontend-only). */
	private readonly stepLog: BattleStepLog = {
		appliedEffects: [],
		enemyDotDamage: 0,
		playerDotDamage: 0,
		enemyHotHeal: 0,
		playerHotHeal: 0
	};

	/** Over-time damage/heal accumulated since the last flush. DoT/HoT applies every 40ms tick (25/s),
	 *  so per-tick logging would flood the feed — instead each channel is summed and emitted as a single
	 *  line per ~1s window (and once more at battle end for the final partial second). */
	private readonly effectDamage = { enemyDot: 0, playerDot: 0, enemyHot: 0, playerHot: 0 };
	private effectDamageElapsedMs = 0;

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
		const enemyData = staticData.enemies ?? [];
		this.timeElapsed = 0;
		this.resetEffectDamage();
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
			for (const { skill, damage, byPlayer } of battleStep(this.player, this.enemy, timeDelta, this.stepLog)) {
				if (byPlayer) {
					logMessage(ELogType.Damage, `You used ${skill.name} and dealt ${formatNum(damage)} damage!`);
				} else {
					logMessage(ELogType.Damage, `${this.enemy.name} used ${skill.name} and dealt ${formatNum(damage)} damage!`);
				}
			}
			this.logEffectApplications();
			this.accumulateEffectDamage(timeDelta);
			if (this.enemy.isDead) {
				this.flushEffectDamage();
				this.setBattleStage(Victorious);
			} else if (this.player.isDead) {
				this.flushEffectDamage();
				this.setBattleStage(Defeated);
				logMessage(ELogType.EnemyDefeated, "You've been defeated!");
			}
		}
		this.timeElapsed += timeDelta;
	}

	/** Logs a line for each effect freshly applied this tick (refreshes are skipped — the chip countdown
	 *  resetting already conveys them). The attribute name follows the shared `.find`-by-id convention
	 *  (#297), falling back to the formatted enum name when the reference set is unavailable. */
	private logEffectApplications() {
		for (const { effect, onPlayer } of this.stepLog.appliedEffects) {
			const name =
				staticData.attributes?.find((attr) => attr.id === effect.attributeId)?.name ??
				attributeEnumName(effect.attributeId);
			logMessage(ELogType.SkillEffect, effectLogMessage(effect, name, onPlayer, this.enemy.name));
		}
	}

	/** Adds this tick's DoT/HoT to the running per-channel totals, flushing a summary line once the
	 *  accumulation window reaches a second. */
	private accumulateEffectDamage(timeDelta: number) {
		this.effectDamage.enemyDot += this.stepLog.enemyDotDamage;
		this.effectDamage.playerDot += this.stepLog.playerDotDamage;
		this.effectDamage.enemyHot += this.stepLog.enemyHotHeal;
		this.effectDamage.playerHot += this.stepLog.playerHotHeal;
		this.effectDamageElapsedMs += timeDelta;
		if (this.effectDamageElapsedMs >= 1000) {
			this.flushEffectDamage();
		}
	}

	/** Emits one combat-log line per non-zero over-time channel accumulated since the last flush, then
	 *  resets the window — so the player sees DoT/HoT totals without a line every 40ms tick. */
	private flushEffectDamage() {
		const { enemyDot, playerDot, enemyHot, playerHot } = this.effectDamage;
		if (enemyDot > 0) {
			logMessage(ELogType.SkillEffect, `${this.enemy.name} took ${formatNum(enemyDot)} damage over time.`);
		}
		if (enemyHot > 0) {
			logMessage(ELogType.SkillEffect, `${this.enemy.name} recovered ${formatNum(enemyHot)} health.`);
		}
		if (playerDot > 0) {
			logMessage(ELogType.SkillEffect, `You took ${formatNum(playerDot)} damage over time.`);
		}
		if (playerHot > 0) {
			logMessage(ELogType.SkillEffect, `You recovered ${formatNum(playerHot)} health.`);
		}
		this.resetEffectDamage();
	}

	private resetEffectDamage() {
		this.effectDamage.enemyDot = 0;
		this.effectDamage.playerDot = 0;
		this.effectDamage.enemyHot = 0;
		this.effectDamage.playerHot = 0;
		this.effectDamageElapsedMs = 0;
	}

	private renderUpdate(renderDelta: number) {
		if (this.stage === Active) {
			this.player.updateRenderCooldowns(renderDelta);
			this.enemy.updateRenderCooldowns(renderDelta);
			this.player.updateRenderEffects(renderDelta);
			this.enemy.updateRenderEffects(renderDelta);
		}
	}

	private setBattleStage(stage: BattleStage) {
		this.stage = stage;
		notifyBattleStageChanged(stage);
	}
}
