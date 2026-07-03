/* The player's live battle-attribute composition — the single assembly the battle engine, the skills
   screen, and the attribute-breakdown screen must all read off, rather than each hand-rolling its own
   copy (#1500). Mirrors the backend's `BattleSnapshot.ToBattler` (and `battler.ts`'s `Battler.reset`):
   allocated stat points + equipped gear form the `BattleAttributes` base, the class locked base and
   proficiency bonuses are composed alongside it — BEFORE the engine's static/derived modifiers, so the
   additive accumulation order matches the backend bit-for-bit — and the class signature passive is added
   LAST, resolved against the fully-assembled set (an attribute-scaled passive reads the final value of
   its scaling attribute, like a skill effect reads its caster).

   Pure over its inputs (mirroring the other display-shared battle helpers in this module) so it stays
   unit-testable independent of the live player/inventory/proficiency stores; each caller supplies its own
   live reads. */

import type { EAttribute, IBattlerAttribute } from '$lib/api';
import { BattleAttributes } from './battle-attributes';
import type { AttributeModifier } from './attribute-modifier';

/** The class locked base + proficiency bonuses, in the order the live battler composes them — the
 *  `additionalModifiers` a caller hands to {@link BattleAttributes.setData} alongside the player's
 *  stat-point allocations and equipped gear. */
export function playerBattleModifiers(
	lockedBaseModifiers: readonly AttributeModifier[],
	proficiencyModifiers: readonly AttributeModifier[]
): AttributeModifier[] {
	return [...lockedBaseModifiers, ...proficiencyModifiers];
}

/** Builds the player's full live {@link BattleAttributes}: allocation + equipped gear, the class locked
 *  base and proficiency bonuses, and the class signature passive composed last against the resolved set.
 *  Any surface displaying live battle numbers must read off this rather than a partial hand-rolled copy,
 *  so a composition change can't silently desync one surface from what the player actually fights with.
 *  `resolveSignaturePassive` is `PlayerManager.battleSignaturePassiveModifier`, threaded through so this
 *  stays pure — it is invoked with a resolver over the just-assembled set, as the live battler does. */
export function composePlayerBattleAttributes(
	attributes: readonly IBattlerAttribute[],
	equipmentStats: readonly IBattlerAttribute[],
	lockedBaseModifiers: readonly AttributeModifier[],
	proficiencyModifiers: readonly AttributeModifier[],
	resolveSignaturePassive: (resolveScalingValue: (attribute: EAttribute) => number) => AttributeModifier
): BattleAttributes {
	const attrs = new BattleAttributes();
	attrs.setData(
		[...attributes, ...equipmentStats],
		true,
		playerBattleModifiers(lockedBaseModifiers, proficiencyModifiers)
	);
	attrs.addModifier(resolveSignaturePassive((attribute) => attrs.getValue(attribute)));
	return attrs;
}
