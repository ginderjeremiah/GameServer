import type { Battler } from './battler';
import type { Skill } from './skill';
import type { Mulberry32 } from '$lib/engine/mulberry32';
import { EAttribute, type ISkillEffect } from '$lib/api';

/**
 * A single skill activation produced by one battle tick: which skill fired, the
 * final damage it dealt after the defender's defense clamp, and whether the
 * player (true) or the enemy (false) was the attacker. The crit/dodged/blocked
 * flags carry the player-only roll outcomes for the combat log: `crit` on a player
 * attack, `dodged`/`blocked` on an incoming enemy attack (a dodged hit is never
 * also blocked). The live BattleEngine turns these into combat-log messages; the
 * headless BattleSimulator ignores them.
 */
export interface SkillActivation {
	skill: Skill;
	damage: number;
	byPlayer: boolean;
	crit: boolean;
	dodged: boolean;
	blocked: boolean;
}

/** A skill effect application that landed during a tick (each application stacks), with the side it
 *  landed on. */
export interface AppliedEffect {
	effect: ISkillEffect;
	/** Whether the effect landed on the player (`true`) or the enemy (`false`). */
	onPlayer: boolean;
	/** The resolved (caster-scaled) magnitude that was applied — what the combat log should report. */
	amount: number;
}

/**
 * Optional per-tick observation sink the live {@link BattleEngine} passes to {@link battleStep} to
 * collect the log-worthy events a tick produces beyond its damage activations: the effects newly
 * applied this tick, and the damage/heal each side took from the end-of-tick DoT/HoT phase. The
 * headless {@link BattleSimulator} omits it, so the parity path computes and allocates nothing extra
 * and stays byte-identical to before. Reused across ticks by the caller, so {@link battleStep} resets
 * it at the start of every call.
 */
export interface BattleStepLog {
	appliedEffects: AppliedEffect[];
	enemyDotDamage: number;
	playerDotDamage: number;
	enemyHotHeal: number;
	playerHotHeal: number;
}

/**
 * Runs one deterministic battle tick — the player's ready skills fire at the
 * enemy, then (only if the enemy survives) the enemy's ready skills fire back —
 * and returns the resulting activations in the order they occurred.
 *
 * This is the single source of the frontend's per-tick battle arithmetic. Both
 * the live {@link BattleEngine} (UI-driven, one call per logical tick) and the
 * headless {@link BattleSimulator} (the parity/test loop) call it, mirroring how
 * the backend's BattleSimulator is the single source its parity test drives.
 * Keeping the exchange here — rather than duplicated in a test — means a change
 * to {@link Battler.advanceCooldowns}, {@link Skill.calculateDamage} or
 * {@link Battler.takeDamage} cannot let the live game diverge from the backend
 * replay while a copied test stays green.
 */
export function battleStep(
	player: Battler,
	enemy: Battler,
	timeDelta: number,
	rng: Mulberry32,
	log?: BattleStepLog
): SkillActivation[] {
	const activations: SkillActivation[] = [];

	// Reset the (reused) observation sink for this tick; absent for the headless simulator.
	if (log) {
		log.appliedEffects.length = 0;
		log.enemyDotDamage = 0;
		log.playerDotDamage = 0;
		log.enemyHotHeal = 0;
		log.playerHotHeal = 0;
	}
	const onApplied = log
		? (effect: ISkillEffect, target: Battler, amount: number) =>
				log.appliedEffects.push({ effect, onPlayer: target === player, amount })
		: undefined;

	// Expire timed effects at the start of the tick, before either side fires, so an effect influences
	// exactly durationMs / tickSize ticks (counting its application tick).
	player.advanceEffects(timeDelta);
	enemy.advanceEffects(timeDelta);

	// Resolve each loadout slot fully in order — accrue, fire, damage, then apply effects — before the
	// next slot accrues, so an earlier slot's effect (e.g. a self CooldownRecovery buff) influences a
	// later slot on the same tick, exactly as the backend's per-slot BattleSkill.Update does. Each fire
	// draws from the shared seeded RNG in a fixed, outcome-independent order (1 crit draw per player fire,
	// then 2 — dodge, block — per enemy fire) so both simulators stay in lockstep.
	player.advanceCooldowns(timeDelta, (skill) => {
		// Player crit: one draw (always), the raw damage multiplied by CriticalDamage BEFORE Defense.
		const crit = rng.next() < player.attributes.getValue(EAttribute.CriticalChance);
		const raw = skill.calculateDamage();
		const damage = enemy.takeDamage(crit ? raw * player.attributes.getValue(EAttribute.CriticalDamage) : raw);
		activations.push({ skill, damage, byPlayer: true, crit, dodged: false, blocked: false });
		skill.applyEffects(enemy, onApplied);
	});

	if (!enemy.isDead) {
		enemy.advanceCooldowns(timeDelta, (skill) => {
			// Dodge then block, both drawn unconditionally (even on a dodge) so the stream never branches on a
			// roll result. A dodge zeroes the hit; a non-dodged block flatly subtracts BlockReduction too.
			const dodged = rng.next() < player.attributes.getValue(EAttribute.DodgeChance);
			const blocked = rng.next() < player.attributes.getValue(EAttribute.BlockChance);
			const raw = skill.calculateDamage();
			let damage = 0;
			if (!dodged) {
				damage = blocked
					? player.takeDamage(raw, player.attributes.getValue(EAttribute.BlockReduction))
					: player.takeDamage(raw);
			}
			activations.push({ skill, damage, byPlayer: false, crit: false, dodged, blocked });
			skill.applyEffects(player, onApplied);
		});
	}

	// End-of-tick damage/heal-over-time, reached only while both battlers still live (mirroring the
	// backend's both-alive guard). The enemy resolves first: DamageTakenPerSecond (bypassing Defense),
	// a death check, then HealthRegenPerSecond (capped at MaxHealth) — so an enemy DoT kill ends the
	// battle before the player's DoT applies, and a same-tick mutual DoT kill leaves the player alive.
	if (!player.isDead && !enemy.isDead) {
		const enemyDot = enemy.applyDamageOverTime(timeDelta);
		if (log) {
			log.enemyDotDamage = enemyDot;
		}
		if (!enemy.isDead) {
			const enemyHot = enemy.applyHealOverTime(timeDelta);
			const playerDot = player.applyDamageOverTime(timeDelta);
			if (log) {
				log.enemyHotHeal = enemyHot;
				log.playerDotDamage = playerDot;
			}
			if (!player.isDead) {
				const playerHot = player.applyHealOverTime(timeDelta);
				if (log) {
					log.playerHotHeal = playerHot;
				}
			}
		}
	}

	return activations;
}
