import type { Battler } from './battler';
import type { Skill } from './skill';

/**
 * A single skill activation produced by one battle tick: which skill fired, the
 * final damage it dealt after the defender's defense clamp, and whether the
 * player (true) or the enemy (false) was the attacker. The live BattleEngine
 * turns these into combat-log messages; the headless BattleSimulator ignores them.
 */
export interface SkillActivation {
	skill: Skill;
	damage: number;
	byPlayer: boolean;
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
export function battleStep(player: Battler, enemy: Battler, timeDelta: number): SkillActivation[] {
	const activations: SkillActivation[] = [];

	// Expire timed effects at the start of the tick, before either side fires, so an effect influences
	// exactly durationMs / tickSize ticks (counting its application tick).
	player.advanceEffects(timeDelta);
	enemy.advanceEffects(timeDelta);

	// Resolve each loadout slot fully in order — accrue, fire, damage, then apply effects — before the
	// next slot accrues, so an earlier slot's effect (e.g. a self CooldownRecovery buff) influences a
	// later slot on the same tick, exactly as the backend's per-slot BattleSkill.Update does.
	player.advanceCooldowns(timeDelta, (skill) => {
		const damage = enemy.takeDamage(skill.calculateDamage());
		activations.push({ skill, damage, byPlayer: true });
		skill.applyEffects(enemy);
	});

	if (!enemy.isDead) {
		enemy.advanceCooldowns(timeDelta, (skill) => {
			const damage = player.takeDamage(skill.calculateDamage());
			activations.push({ skill, damage, byPlayer: false });
			skill.applyEffects(player);
		});
	}

	// End-of-tick damage/heal-over-time, reached only while both battlers still live (mirroring the
	// backend's both-alive guard). The enemy resolves first: DamageTakenPerSecond (bypassing Defense),
	// a death check, then HealthRegenPerSecond (capped at MaxHealth) — so an enemy DoT kill ends the
	// battle before the player's DoT applies, and a same-tick mutual DoT kill leaves the player alive.
	if (!player.isDead && !enemy.isDead) {
		enemy.applyDamageOverTime(timeDelta);
		if (!enemy.isDead) {
			enemy.applyHealOverTime(timeDelta);
			player.applyDamageOverTime(timeDelta);
			if (!player.isDead) {
				player.applyHealOverTime(timeDelta);
			}
		}
	}

	return activations;
}
