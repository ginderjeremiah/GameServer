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
// additive-then-multiplicative path as stat allocations, so they participate identically on both sides.
//
// The values are asserted **bit-exactly** (toBe / Assert.Equal without tolerance): this is an anti-cheat
// parity surface, so the two simulators must agree to the last bit, not merely within a tolerance. A loose
// tolerance previously masked the ordering divergence fixed in #1189 — the proficiency modifiers must compose
// with the base set BEFORE the static engine modifiers (mirroring the backend) so the additive accumulation
// order is identical; the `maxHealthDerivedAdditive` scenario lands an additive bonus on a derived attribute
// (MaxHealth, which carries a static additive base) to pin exactly that order.
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
		},

		// Additive bonus on a DERIVED attribute (MaxHealth = 50 + 20·Endurance + 5·Strength). This is the case
		// the core-attribute scenarios above can't exercise: MaxHealth carries a static additive base, so the
		// proficiency additive must accumulate in the same order relative to those statics on both sides. The
		// allocations (Endurance 34, Strength 59) and bonus (3.14) are chosen so the order is observable in the
		// last bits — MaxHealth = 3.14 + 50 + 680 + 295 = 1028.1399999999999, which differs from the buggy
		// "statics first" order (1028.14) past the 10th decimal. The expectation is written as the exact double
		// the canonical (backend) order produces, so a regression to the old ordering fails this row.
		maxHealthDerivedAdditive: {
			allocations: [
				[EAttribute.Endurance, 34],
				[EAttribute.Strength, 59]
			],
			levels: [{ level: 1, modifiers: [[EAttribute.MaxHealth, EModifierType.Additive, 3.14]] }],
			playerLevel: 1,
			expected: [[EAttribute.MaxHealth, 1028.1399999999999]]
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

	it.each(Object.keys(scenarios))('composes %s proficiency bonuses onto attributes', (name) => {
		const { allocations, levels, playerLevel, expected } = scenarios[name];
		// Compose through the production path: setData places the proficiency modifiers with the base set,
		// BEFORE the static engine modifiers, exactly as Battler.reset feeds them in a live battle and as the
		// backend's BattleSnapshot.GetModifiers + AttributeCollection do. This is what makes the additive
		// accumulation order identical across FE/BE; adding them afterwards (the old approach) diverges on
		// derived attributes that carry a static additive base (#1189).
		const ba = new BattleAttributes();
		ba.setData(makeAttrs(...allocations), true, proficiencyModifiers(toLevelModifiers(levels), playerLevel));

		for (const [attribute, value] of expected) {
			expect(ba.getValue(attribute)).toBe(value);
		}
	});
});
