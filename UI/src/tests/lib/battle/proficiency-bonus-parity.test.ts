import { describe, it, expect, vi } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { EModifierType } from '$lib/battle/attribute-modifier';
import { proficiencyModifiers, type ProficiencyLevelModifier } from '$lib/battle/proficiency-modifiers';

// BattleAttributes resolves attribute names through the static-data store; an empty mock is enough here
// since the parity assertions read raw values, not names.
const { mockAttributes } = vi.hoisted(() => ({ mockAttributes: [] as IAttribute[] }));
vi.mock('$stores', () => ({
	staticData: {
		get attributes() {
			return mockAttributes;
		}
	}
}));

// Parity guard for the proficiency attribute-bonus pipeline (spike #982 area E). Every scenario here MUST be
// mirrored — with identical inputs (the same name, allocations, authored level payouts, and player level) and
// identical expected attribute values — in the backend suite
// Game.Core.Tests/Attributes/ProficiencyBonusParityTests.cs. The proficiency bonuses compose through the same
// additive-then-multiplicative path as stat allocations, so they participate identically on both sides; the
// expectations are asserted on the core attributes the bonuses land on directly (stable literals, independent
// of the derived-stat coefficients the BattleAttributes parity suite already pins).
describe('proficiency bonus (parity)', () => {
	type Allocation = [EAttribute, number];
	type ModifierSpec = [EAttribute, EModifierType, number];
	interface LevelSpec {
		level: number;
		modifiers: ModifierSpec[];
	}
	interface Scenario {
		allocations: Allocation[];
		levels: LevelSpec[];
		playerLevel: number;
		expected: [EAttribute, number][];
	}

	const scenarios: Record<string, Scenario> = {
		// Cumulative additive: the level-1 and level-2 Strength payouts both apply at level 2, the far-off
		// level-5 payout does not. Strength = 0 (alloc) + 4 + 6 = 10.
		cumulativeAdditive: {
			allocations: [],
			levels: [
				{ level: 1, modifiers: [[EAttribute.Strength, EModifierType.Additive, 4]] },
				{ level: 2, modifiers: [[EAttribute.Strength, EModifierType.Additive, 6]] },
				{ level: 5, modifiers: [[EAttribute.Strength, EModifierType.Additive, 100]] }
			],
			playerLevel: 2,
			expected: [[EAttribute.Strength, 10]]
		},

		// Additive then multiplicative: the proficiency additive sums with the allocation before the
		// proficiency multiplicative scales the total. Strength = (10 + 5) * 1.5 = 22.5.
		additiveThenMultiplicative: {
			allocations: [[EAttribute.Strength, 10]],
			levels: [
				{ level: 1, modifiers: [[EAttribute.Strength, EModifierType.Additive, 5]] },
				{ level: 2, modifiers: [[EAttribute.Strength, EModifierType.Multiplicative, 1.5]] }
			],
			playerLevel: 2,
			expected: [[EAttribute.Strength, 22.5]]
		},

		// Below every payout: a player under the first payout level gets no bonus. Strength = 7 (alloc only).
		belowEveryPayout: {
			allocations: [[EAttribute.Strength, 7]],
			levels: [{ level: 3, modifiers: [[EAttribute.Strength, EModifierType.Additive, 5]] }],
			playerLevel: 2,
			expected: [[EAttribute.Strength, 7]]
		},

		// Multiple attributes from one payout level, each landing on its own attribute.
		multiAttributePayout: {
			allocations: [],
			levels: [
				{
					level: 1,
					modifiers: [
						[EAttribute.Strength, EModifierType.Additive, 3],
						[EAttribute.Endurance, EModifierType.Additive, 8]
					]
				}
			],
			playerLevel: 1,
			expected: [
				[EAttribute.Strength, 3],
				[EAttribute.Endurance, 8]
			]
		}
	};

	const makeAttrs = (...pairs: Allocation[]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

	const toLevelModifiers = (levels: LevelSpec[]): ProficiencyLevelModifier[] =>
		levels.flatMap((spec) =>
			spec.modifiers.map(([attributeId, modifierTypeId, amount]) => ({
				level: spec.level,
				attributeId,
				modifierTypeId,
				amount
			}))
		);

	it.each(Object.keys(scenarios))('composes %s proficiency bonuses onto core attributes', (name) => {
		const { allocations, levels, playerLevel, expected } = scenarios[name];
		// Use the real (derived) composition path — like the backend's AttributeCollection — so the
		// additive-then-multiplicative ordering is honoured. Core attributes carry no static modifier, so the
		// derived pass leaves the asserted values as allocation + proficiency bonus alone.
		const ba = new BattleAttributes(makeAttrs(...allocations));

		for (const modifier of proficiencyModifiers(toLevelModifiers(levels), playerLevel)) {
			ba.addModifier(modifier);
		}

		for (const [attribute, value] of expected) {
			expect(ba.getValue(attribute)).toBeCloseTo(value, 10);
		}
	});
});
