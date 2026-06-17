import { Battler, battleStep, type BattleStepLog } from '$lib/battle';
import { Mulberry32 } from '$lib/engine/mulberry32';
import { staticData } from '$stores';
import { ELogType, IEnemyInstance } from '$lib/api';
import { logMessage, type LogOutcome } from '../log';
import { formatNum, createHook, Action, effectLogMessage, attributeIsHarmful, attributeName } from '$lib/common';
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

	/** The seeded battle RNG, re-created from each enemy's seed in {@link reset} so the live battle draws the
	 *  crit/dodge/block rolls from the same stream the backend replays. Seeded to 0 until the first reset. */
	private rng = new Mulberry32(0);

	private logicalUnhook?: Action;
	private renderUnhook?: Action;
	private enemyLoadedUnhook?: Action;
	/** Tears down the in-flight post-victory loading countdown (removes its render hook and resolves the
	 *  awaited promise); undefined when no countdown is active. */
	private finishLoading?: Action;

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
			// Tear down to the clean Idle baseline. Leaving a transient stage (e.g. the post-victory
			// Loading cooldown, or Paused mid boss-swap) would strand the fight on the next start():
			// the enemy manager only re-fetches from Idle/Victorious/Defeated, while Loading/Paused make
			// no progress on their own. Resetting here lets a return from the admin screen resume cleanly.
			this.setBattleStage(Idle);
		}
		// A loading countdown is driven by the render engine independently of `running`, so cancel it
		// unconditionally — otherwise its render hook leaks and the awaiting getNewEnemy path hangs.
		this.finishLoading?.();
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
		// Re-arming the battle cancels any in-flight post-victory cooldown so its render hook is removed
		// and the awaiting caller is released rather than stranded mid-countdown.
		this.finishLoading?.();
		this.timeElapsed = 0;
		// Seed the battle RNG from the enemy instance's seed (the same value the backend simulates against),
		// so the crit/dodge/block draws stay in lockstep with the anti-cheat replay.
		this.rng = new Mulberry32(enemyInstance.seed);
		this.resetEffectDamage();
		this.player.reset(playerManager, inventoryManager.equipmentStats);
		this.enemy.reset({ ...enemyInstance, ...enemyData[enemyInstance.id] });
		this.resume();
	};

	public getOpponent(battler: Battler) {
		return battler.id === this.player.id ? this.enemy : this.player;
	}

	public startLoading(loadingTime: number) {
		// Cancel any countdown still in flight so re-invoking can't leak the previous render hook.
		this.finishLoading?.();
		this.loadingTime = loadingTime;
		this.setBattleStage(Loading);
		const { promise, resolve } = Promise.withResolvers<void>();
		const unhook = onRenderUpdate((delta) => {
			this.loadingTime -= delta;
			if (this.loadingTime <= 0) {
				this.finishLoading?.();
			}
		}, false);
		// Single idempotent teardown shared by the natural countdown-complete path and the stop()/reset()
		// cancellation path: remove the render hook and release the awaiting caller exactly once.
		this.finishLoading = () => {
			this.finishLoading = undefined;
			unhook();
			resolve();
		};
		return promise;
	}

	private logicalUpdate(timeDelta: number) {
		if (this.stage === Active) {
			for (const { skill, damage, byPlayer, crit, dodged, blocked } of battleStep(
				this.player,
				this.enemy,
				timeDelta,
				this.rng,
				this.stepLog
			)) {
				const outcome = damageLogOutcome(byPlayer, crit, dodged, blocked);
				logMessage(ELogType.Damage, this.damageLogMessage(skill.name, damage, outcome), outcome);
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

	/** Builds the combat-log line for one skill activation, surfacing the player-only crit/dodge/block
	 *  outcomes (#178). The message prose and the structured `outcome` are derived from the same
	 *  {@link damageLogOutcome} decision, so a reworded line can no longer drift from the glyph
	 *  `logKind` picks for it (the glyph now keys off `outcome`, not this text — see `log-kind.ts`). */
	private damageLogMessage(skillName: string, damage: number, outcome: LogOutcome): string {
		switch (outcome) {
			case 'player-crit':
				return `You landed a critical hit with ${skillName} for ${formatNum(damage)} damage!`;
			case 'player-hit':
				return `You used ${skillName} and dealt ${formatNum(damage)} damage!`;
			case 'player-dodge':
				return `You dodged ${this.enemy.name}'s ${skillName}!`;
			case 'player-block':
				return `You blocked ${this.enemy.name}'s ${skillName}, taking only ${formatNum(damage)} damage!`;
			case 'enemy-hit':
				return `${this.enemy.name} used ${skillName} and dealt ${formatNum(damage)} damage!`;
		}
	}

	/** Logs a line for each effect freshly applied this tick (refreshes are skipped — the chip countdown
	 *  resetting already conveys them). The attribute name follows the shared `.find`-by-id convention
	 *  (#297), falling back to the formatted enum name when the reference set is unavailable. */
	private logEffectApplications() {
		for (const { effect, onPlayer } of this.stepLog.appliedEffects) {
			const name = attributeName(effect.attributeId, staticData.attributes);
			const isHarmful = attributeIsHarmful(effect.attributeId, staticData.attributes);
			logMessage(ELogType.SkillEffect, effectLogMessage(effect, name, isHarmful, onPlayer, this.enemy.name));
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

/** Resolves the structured {@link LogOutcome} for one damage exchange from the battle-step flags.
 *  The crit/dodge/block flags are only ever set on the player's side (crit on the player's own hit;
 *  dodge/block on an incoming enemy hit — a dodged hit is never also blocked), so the mapping is
 *  unambiguous. Drives both the log prose and the glyph, keeping them in lockstep. */
function damageLogOutcome(byPlayer: boolean, crit: boolean, dodged: boolean, blocked: boolean): LogOutcome {
	if (byPlayer) {
		return crit ? 'player-crit' : 'player-hit';
	}
	if (dodged) {
		return 'player-dodge';
	}
	if (blocked) {
		return 'player-block';
	}
	return 'enemy-hit';
}
