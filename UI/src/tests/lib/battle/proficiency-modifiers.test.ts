import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { EModifierType, EAttributeModifierSource } from '$lib/battle/attribute-modifier';
import { proficiencyModifiers, type ProficiencyLevelModifier } from '$lib/battle/proficiency-modifiers';

// Mirrors Game.Core.Tests/Proficiencies/ProficiencyModifiersTests.cs — the proficiency-bonus → battle
// attribute-modifier conversion: a player's total bonus is the cumulative set of every authored payout level
// at or below their current level, each stamped with the Proficiency source.
describe('proficiencyModifiers', () => {
	const mod = (
		level: number,
		attributeId: EAttribute,
		amount: number,
		modifierTypeId: EModifierType = EModifierType.Additive
	): ProficiencyLevelModifier => ({ level, attributeId, modifierTypeId, amount });

	it('sums every authored payout at or below the level and stamps the Proficiency source', () => {
		const levels = [mod(1, EAttribute.Strength, 5), mod(2, EAttribute.Strength, 3), mod(3, EAttribute.Endurance, 10)];

		// At level 2 the player has earned the level-1 and level-2 payouts, but not level 3.
		const result = proficiencyModifiers(levels, 2);

		expect(result.map((m) => m.amount)).toEqual([5, 3]);
		expect(result.every((m) => m.source === EAttributeModifierSource.Proficiency)).toBe(true);
		expect(result.every((m) => m.attribute === EAttribute.Strength)).toBe(true);
	});

	it('yields nothing below every payout level', () => {
		expect(proficiencyModifiers([mod(3, EAttribute.Strength, 5)], 2)).toEqual([]);
	});

	it('includes a payout authored at level 0', () => {
		const result = proficiencyModifiers([mod(0, EAttribute.Strength, 7)], 0);
		expect(result).toHaveLength(1);
		expect(result[0].amount).toBe(7);
	});

	it('preserves each payout modifier type', () => {
		const result = proficiencyModifiers(
			[
				mod(1, EAttribute.Strength, 5, EModifierType.Additive),
				mod(2, EAttribute.Strength, 1.5, EModifierType.Multiplicative)
			],
			2
		);
		expect(result[0].type).toBe(EModifierType.Additive);
		expect(result[1].type).toBe(EModifierType.Multiplicative);
	});
});
