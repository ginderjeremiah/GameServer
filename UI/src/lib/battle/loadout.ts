/* loadout.ts — the frontend mirror of the backend's `Game.Core.Battle.BattleLoadout` (spike #1342).

   The weapon-match gate is parity-critical: a weapon-leaf-typed skill (Sword/Axe/Bow/Club/Dagger/Unarmed)
   is fielded only while the matching weapon is equipped; weapon-agnostic types are always fielded. The pure
   predicate {@link isFielded} is computed once at battler assembly (`Battler.fillSkills`), never on the
   deterministic tick loop, so it adds no RNG-coupled parity surface. The Skills-screen grey-out and the
   inventory pre-swap warning derive from the *same* predicate, so the dimmed set the player sees can never
   diverge from what the battle actually fields. */

import { EDamageType, type ISkillDamagePortion } from '$lib/api';
import { isWeaponLeaf, primaryDamageType } from './damage-types';

/** A skill viewed only through its damage split — the shape both the contract `ISkill` and the battle `Skill`
 *  satisfy, so the gate helpers work over either without coupling to the heavier types. */
export interface WeaponGatedSkill {
	readonly damagePortions: readonly ISkillDamagePortion[];
}

/** The weapon-match gate (spike #1342): whether a skill of leaf type `skillType` is fielded while
 *  `equippedWeaponType` is wielded. A weapon-leaf-typed skill is fielded only when it matches the equipped
 *  weapon type (exact leaf-match); a weapon-agnostic type is always fielded. Mirrors the backend
 *  `BattleLoadout.IsFielded` exactly. */
export function isFielded(skillType: EDamageType, equippedWeaponType: EDamageType): boolean {
	return !isWeaponLeaf(skillType) || skillType === equippedWeaponType;
}

/** True when a skill is dimmed (dormant) by the weapon-match gate for the equipped weapon: a weapon-leaf-typed
 *  skill whose type doesn't match the equipped weapon. Weapon-agnostic skills are never dormant. The grey-out
 *  derivation for the Skills screen. */
export function isSkillDormant(skill: WeaponGatedSkill, equippedWeaponType: EDamageType): boolean {
	return !isFielded(primaryDamageType(skill.damagePortions), equippedWeaponType);
}

/** The skills from `skills` that go dormant when the equipped weapon changes from `currentWeaponType` to
 *  `nextWeaponType` — i.e. fielded now, dimmed after the swap. A skill already dormant under the current
 *  weapon isn't re-listed, so the result is exactly the loadout entries the swap would newly dim. Drives the
 *  inventory pre-swap warning. */
export function newlyDormantSkills<T extends WeaponGatedSkill>(
	skills: readonly T[],
	currentWeaponType: EDamageType,
	nextWeaponType: EDamageType
): T[] {
	return skills.filter((s) => isSkillDormant(s, nextWeaponType) && !isSkillDormant(s, currentWeaponType));
}
