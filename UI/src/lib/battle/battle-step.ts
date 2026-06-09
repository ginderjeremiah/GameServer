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

	for (const skill of player.advanceCooldowns(timeDelta)) {
		const damage = enemy.takeDamage(skill.calculateDamage());
		activations.push({ skill, damage, byPlayer: true });
	}

	if (!enemy.isDead) {
		for (const skill of enemy.advanceCooldowns(timeDelta)) {
			const damage = player.takeDamage(skill.calculateDamage());
			activations.push({ skill, damage, byPlayer: false });
		}
	}

	return activations;
}
