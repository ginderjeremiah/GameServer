import { Battler, battleStep, resistanceTotal, type BattleStepLog, type AttributeModifier } from '$lib/battle';
import { Mulberry32 } from '$lib/engine/mulberry32';
import { staticData, playerProficiencies } from '$stores';
import { ELogType, EDamageType, IBattlerAttribute, IEnemyInstance } from '$lib/api';
import { DEFAULT_MAX_BATTLE_MS } from '$lib/api/types/game-constants';
import { logMessage } from '../log';
import {
	formatNum,
	createHook,
	Action,
	effectLogMessage,
	attributeIsHarmful,
	attributeName,
	damageLogMessage,
	reflectLogMessage,
	classifyResist,
	type DirectHitOutcome,
	type ReflectOutcome
} from '$lib/common';
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
	Paused,
	/** The 2-minute battle cap was reached with both battlers alive — a stalemate draw (no rewards). */
	Drawn
}

const { Idle, Active, Victorious, Defeated, Loading, Paused, Drawn } = BattleStage;

const battleStageChangedHook = createHook<[BattleStage]>();
const notifyBattleStageChanged = battleStageChangedHook.notify;
export const onBattleStageChanged = battleStageChangedHook.onNotified;

/** One combat outcome surfaced for the fight screen's floating numbers. `target` is the battler the
 *  float spawns over (the side that was struck / defended); `kind` picks its label, and `amount` is the
 *  damage to show — omitted for a dodge, which has no number; a negative amount is an absorbed hit's net
 *  heal. `damageType` (present for a damaging `hit`/`crit`) tints the number and picks its type glyph
 *  (#1320, Area F); a `dodge` has no number and a `reflect` is raw/untyped, so both omit it. A `reflect`
 *  (#1330) spawns over the original attacker — the side that took the returned damage — and carries no
 *  type. Player-only crit/dodge mirror the battle-step roll surface (the enemy never dodges/crits). */
export interface CombatFloatEvent {
	target: 'player' | 'enemy';
	kind: 'hit' | 'crit' | 'dodge' | 'reflect';
	amount?: number;
	damageType?: EDamageType;
}

const combatFloatHook = createHook<[CombatFloatEvent]>();
const notifyCombatFloat = combatFloatHook.notify;
/** Subscribe to per-activation combat outcomes for the floating-number layer. Fires once per skill
 *  activation during the live battle loop; the headless simulator never emits these. */
export const onCombatFloat = combatFloatHook.onNotified;

/** A reference snapshot of the player's battle-relevant derivation inputs — the same inputs the player
 *  {@link Battler} is derived from. Captured by {@link BattleEngine.capturePlayerBattleState} and compared
 *  by {@link BattleEngine.playerBattleStateMatches}. Used by the idle loop to tell whether the player
 *  changed their build during a post-battle cooldown, in which case a server-prefetched next battle
 *  (snapshotted at the previous battle's end) would no longer match what the frontend derives. */
export interface PlayerBattleState {
	equipmentStats: IBattlerAttribute[];
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	level: number;
	lockedBaseModifiers: AttributeModifier[];
	proficiencyModifiers: AttributeModifier[];
}

export class BattleEngine {
	public stage = Idle;
	public player: Battler = new Battler();
	public enemy: Battler = new Battler();
	public timeElapsed = 0;
	public loadingTime = 0;
	/** The full enemy-cooldown duration the current countdown started from, so the UI can render the
	 *  remaining {@link loadingTime} as a fraction. Only meaningful while {@link stage} is Loading. */
	public loadingTotal = 0;
	public running = false;

	/** The seeded battle RNG, re-created from each enemy's seed in {@link reset} so the live battle draws the
	 *  crit/dodge/block rolls from the same stream the backend replays. Seeded to 0 until the first reset. */
	private rng = new Mulberry32(0);

	private logicalUnhook?: Action;
	private renderUnhook?: Action;
	private enemyLoadedUnhook?: Action;
	/** Tears down the in-flight post-victory loading countdown (removes its logical hook and resolves the
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

	/** The inputs of the last *full* player attribute derive — the equipment stats, attribute
	 *  distribution, loadout, and level. An idle farm re-spawns with all four unchanged, so the
	 *  per-enemy reset can re-arm the existing player battler instead of rebuilding the whole attribute
	 *  graph (and every Skill) from scratch on each cooldown (#811). The arrays are compared by
	 *  reference — every producer reassigns rather than mutates in place, so a reference match means the
	 *  inputs are unchanged. */
	private lastEquipmentStats?: IBattlerAttribute[];
	private lastPlayerAttributes?: IBattlerAttribute[];
	private lastSelectedSkills?: number[];
	private lastPlayerLevel?: number;
	private lastLockedBaseModifiers?: AttributeModifier[];
	private lastProficiencyModifiers?: AttributeModifier[];

	public start() {
		if (!this.running) {
			this.running = true;
			this.logicalUnhook = onLogicalUpdate((delta) => this.logicalUpdate(delta));
			this.renderUnhook = onRenderUpdate((_, logicalDelta) => this.renderUpdate(logicalDelta));
			this.enemyLoadedUnhook = onNewEnemyLoaded((enemy) => this.reset(enemy));
			this.resetPlayer();
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
		// A loading countdown is driven by the logical engine independently of `running`, so cancel it
		// unconditionally — otherwise its logical hook leaks and the awaiting getNewEnemy path hangs.
		this.finishLoading?.();
	}

	public pause() {
		this.setBattleStage(Paused);
	}

	/**
	 * Drops the live battle to its Idle baseline without tearing down the loop hooks — used when the player
	 * parks in the no-combat Home zone (the idle loop is halted separately so no new enemy spawns). The
	 * in-flight fight simply stops: {@link timeElapsed} is reset to 0 so the next real battle resolves the
	 * now-orphaned backend battle as an abandon with no outcome (the player forfeits the unfinished fight by
	 * walking home), and the fight screen shows the resting state.
	 */
	public rest() {
		this.finishLoading?.();
		this.timeElapsed = 0;
		this.setBattleStage(Idle);
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
		this.resetPlayer();
		this.enemy.reset({ ...enemyInstance, ...enemyData[enemyInstance.id] });
		this.resume();
	};

	/** Resets the player battler for a new enemy, re-deriving the full attribute graph only when the
	 *  equipment / attributes / loadout / level changed since the last derive; otherwise re-arms the
	 *  existing battler (clears effects, resets health + skill charges) without the full rebuild (#811). */
	private resetPlayer() {
		const equipmentStats = inventoryManager.equipmentStats;
		const attributes = playerManager.attributes;
		const selectedSkills = playerManager.selectedSkills;
		const level = playerManager.level;
		// Proficiency level bonuses, composed from the player's levels against the reference data. A stable
		// reference while unchanged (a `$derived`), so an idle farm whose proficiencies didn't change re-arms
		// the existing battler instead of rebuilding it (#811).
		const proficiencyModifiers = playerProficiencies.battleModifiers;
		// The class locked base — the level-scaled attribute fingerprint (spike #1126 area D). A stable
		// reference while the level and class distribution are unchanged (memoised on the manager), so an idle
		// farm re-arms the existing battler instead of rebuilding it (#811).
		const lockedBaseModifiers = playerManager.battleLockedBaseModifiers;
		if (
			equipmentStats === this.lastEquipmentStats &&
			attributes === this.lastPlayerAttributes &&
			selectedSkills === this.lastSelectedSkills &&
			level === this.lastPlayerLevel &&
			lockedBaseModifiers === this.lastLockedBaseModifiers &&
			proficiencyModifiers === this.lastProficiencyModifiers
		) {
			this.player.reset();
			return;
		}
		// Granted skills change only when equipment does, so gating the re-derive on equipmentStats (above)
		// already covers them; read the slot-ordered ids here so the rebuilt battler fields them. The locked
		// base composes before the proficiency bonuses (the order the backend BattleSnapshot.GetModifiers uses)
		// so the additive accumulation — and therefore the result down to the last bit — matches the replay.
		// equippedWeaponType drives the weapon-match gate (#1342): only the player battler is gated; the enemy
		// rebuild below passes no weapon type and fields its full authored loadout.
		this.player.reset(
			playerManager,
			equipmentStats,
			inventoryManager.grantedSkillIds,
			[...lockedBaseModifiers, ...proficiencyModifiers],
			inventoryManager.equippedWeaponType
		);
		// Compose the class signature passive LAST — after the locked base, proficiency bonuses, and the static
		// engine modifiers the rebuild just assembled — so an attribute-scaled passive reads the fully-resolved
		// value of its scaling attribute (snapshot state, like a skill effect reads its caster) and lands in the
		// same per-attribute apply order the backend's BattleSnapshot.ToBattler appends it in; float addition is
		// not associative, so the anti-cheat replay depends on that order matching. The data-less re-arm above
		// keeps this modifier (setData isn't re-run), so it is composed only here, on a full rebuild.
		this.player.attributes.addModifier(
			playerManager.battleSignaturePassiveModifier((attribute) => this.player.attributes.getValue(attribute))
		);
		this.lastEquipmentStats = equipmentStats;
		this.lastPlayerAttributes = attributes;
		this.lastSelectedSkills = selectedSkills;
		this.lastPlayerLevel = level;
		this.lastLockedBaseModifiers = lockedBaseModifiers;
		this.lastProficiencyModifiers = proficiencyModifiers;
	}

	public getOpponent(battler: Battler) {
		return battler.id === this.player.id ? this.enemy : this.player;
	}

	/** Captures the player's current battle-relevant derivation inputs (the same inputs {@link resetPlayer}
	 *  derives the player {@link Battler} from). The idle loop captures this at a battle's end to later detect
	 *  whether the player changed their build during the post-battle cooldown. */
	public capturePlayerBattleState(): PlayerBattleState {
		return {
			equipmentStats: inventoryManager.equipmentStats,
			attributes: playerManager.attributes,
			selectedSkills: playerManager.selectedSkills,
			level: playerManager.level,
			lockedBaseModifiers: playerManager.battleLockedBaseModifiers,
			proficiencyModifiers: playerProficiencies.battleModifiers
		};
	}

	/** Whether the player's battle-relevant inputs are unchanged since {@link capturePlayerBattleState}
	 *  produced `state`. Compared by reference (every producer reassigns rather than mutates in place, per
	 *  #811), so a match guarantees the player {@link Battler} the frontend would derive is identical to the
	 *  snapshot the server prefetched the next battle against — i.e. the bundled battle is parity-safe. */
	public playerBattleStateMatches(state: PlayerBattleState): boolean {
		return (
			state.equipmentStats === inventoryManager.equipmentStats &&
			state.attributes === playerManager.attributes &&
			state.selectedSkills === playerManager.selectedSkills &&
			state.level === playerManager.level &&
			state.lockedBaseModifiers === playerManager.battleLockedBaseModifiers &&
			state.proficiencyModifiers === playerProficiencies.battleModifiers
		);
	}

	public startLoading(loadingTime: number) {
		// Cancel any countdown still in flight so re-invoking can't leak the previous logical hook.
		this.finishLoading?.();
		this.loadingTime = loadingTime;
		this.loadingTotal = loadingTime;
		this.setBattleStage(Loading);
		const { promise, resolve } = Promise.withResolvers<void>();
		// Drive the countdown off the logical clock, not the render (rAF) loop: rAF is suspended while the
		// tab is backgrounded, which would freeze the cooldown and park the farm loop in Loading until the
		// tab is refocused (#1366). The logical loop keeps running (throttled, with catch-up) when hidden.
		const unhook = onLogicalUpdate((delta) => {
			this.loadingTime -= delta;
			if (this.loadingTime <= 0) {
				this.finishLoading?.();
			}
		});
		// Single idempotent teardown shared by the natural countdown-complete path and the stop()/reset()
		// cancellation path: remove the logical hook and release the awaiting caller exactly once.
		this.finishLoading = () => {
			this.finishLoading = undefined;
			unhook();
			resolve();
		};
		return promise;
	}

	private logicalUpdate(timeDelta: number) {
		// Advance the battle clock only while a battle is actually live. Between battles (the Loading
		// cooldown, Paused mid-swap, or resting in the no-combat Home zone) it stays frozen, so a value later
		// read as an abandon's client-fought duration reflects only real combat time — never time parked idle.
		// Accumulate first so the timeout check below evaluates against this tick's elapsed time: the live
		// loop must declare the draw on the same tick the headless BattleSimulator caps at (battle parity).
		if (this.stage === Active) {
			this.timeElapsed += timeDelta;
			for (const { skill, damage, byPlayer, crit, dodged, reflected } of battleStep(
				this.player,
				this.enemy,
				timeDelta,
				this.rng,
				this.stepLog
			)) {
				const outcome = damageLogOutcome(byPlayer, crit, dodged);
				const damageType = skill.primaryDamageType;
				// Classify the hit's resist outcome from the defender's live resistance to its type (a dodged hit
				// never resolved, so it can't be resisted). Computed here, not in the parity-critical battleStep,
				// so the headless simulator stays byte-identical — like the existing crit/dodge log flags.
				const resist = dodged
					? 'normal'
					: classifyResist(resistanceTotal(damageType, (byPlayer ? this.enemy : this.player).attributes), damage);
				logMessage(
					ELogType.Damage,
					damageLogMessage(skill.name, damage, outcome, damageType, resist, this.enemy.name),
					outcome,
					resist === 'normal' ? undefined : resist
				);
				notifyCombatFloat(combatFloatEvent(outcome, damage, damageType));
				// Deterministic reflection (#1330): the defender returned part of this hit to the attacker. The
				// reflector is the opposite side of the activation — a player hit is reflected by the enemy, and an
				// enemy hit by the player — and it deals raw, untyped damage, so the line carries no resist note.
				if (reflected > 0) {
					const reflectOutcome: ReflectOutcome = byPlayer ? 'enemy-reflect' : 'player-reflect';
					logMessage(ELogType.Damage, reflectLogMessage(reflectOutcome, reflected, this.enemy.name), reflectOutcome);
					// Float the returned damage over the original attacker (the side that took it): a player hit
					// reflected by the enemy lands back on the player, an enemy hit reflected by the player on the enemy.
					notifyCombatFloat({ target: byPlayer ? 'player' : 'enemy', kind: 'reflect', amount: reflected });
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
			} else if (this.timeElapsed >= DEFAULT_MAX_BATTLE_MS) {
				// Neither side landed the kill within the 2-minute cap: a true stalemate ends as a draw (no
				// rewards). Death is checked first, so this only fires with both battlers alive — mirroring the
				// headless simulator's non-victory timeout return, keeping the live loop in FE/BE parity.
				this.flushEffectDamage();
				this.setBattleStage(Drawn);
				logMessage(ELogType.EnemyDefeated, 'Stalemate! The battle reached the time limit and ended in a draw.');
			}
		}
	}

	/** Logs a line for each effect applied this tick. Effects now stack — every application is a genuine
	 *  new entry that raises the total and the chip's stack count — so each is logged. The attribute name
	 *  follows the shared `.find`-by-id convention (#297), falling back to the formatted enum name when
	 *  the reference set is unavailable. */
	private logEffectApplications() {
		for (const { effect, onPlayer, amount } of this.stepLog.appliedEffects) {
			const name = attributeName(effect.attributeId, staticData.attributes);
			const isHarmful = attributeIsHarmful(effect.attributeId, staticData.attributes);
			logMessage(ELogType.SkillEffect, effectLogMessage(effect, name, isHarmful, onPlayer, this.enemy.name, amount));
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
 *  The crit/dodge flags are only ever set on the player's side (crit on the player's own hit; dodge
 *  on an incoming enemy hit), so the mapping is unambiguous. Drives both the log prose and the glyph,
 *  keeping them in lockstep. */
function damageLogOutcome(byPlayer: boolean, crit: boolean, dodged: boolean): DirectHitOutcome {
	if (byPlayer) {
		return crit ? 'player-crit' : 'player-hit';
	}
	if (dodged) {
		return 'player-dodge';
	}
	return 'enemy-hit';
}

/** Maps one activation's resolved {@link LogOutcome} to the float that spawns for it, so the float and
 *  the combat-log line are both driven by the single {@link damageLogOutcome} classifier rather than a
 *  parallel copy of the flag branching. A player hit/crit floats over the enemy; an incoming enemy hit
 *  floats over the player as a dodge (no number) or a plain hit. `damageType` rides every damaging
 *  kind (not a dodge) so the floater can tint by type (#1320, Area F). */
function combatFloatEvent(outcome: DirectHitOutcome, damage: number, damageType: EDamageType): CombatFloatEvent {
	switch (outcome) {
		case 'player-crit':
			return { target: 'enemy', kind: 'crit', amount: damage, damageType };
		case 'player-hit':
			return { target: 'enemy', kind: 'hit', amount: damage, damageType };
		case 'player-dodge':
			return { target: 'player', kind: 'dodge' };
		case 'enemy-hit':
			return { target: 'player', kind: 'hit', amount: damage, damageType };
	}
}
