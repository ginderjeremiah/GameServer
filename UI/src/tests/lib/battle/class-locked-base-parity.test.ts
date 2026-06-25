import { describe, it, expect, vi } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { classLockedBaseModifiers, type ClassAttributeDistribution } from '$lib/battle/class-modifiers';

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

// Parity guard for the class locked-base attribute pipeline (spike #1126 area D). Every scenario here MUST be
// mirrored — with identical inputs (the same allocations, class attribute distributions, and level) and
// identical expected attribute values — in the backend suite
// Game.Core.Tests/Attributes/ClassLockedBaseParityTests.cs. The locked base composes through the same
// additive-then-multiplicative path as stat allocations and proficiency bonuses, so it participates
// identically on both sides — and sits in the same place in the modifier order (with the base set, before the
// static engine modifiers), since floating-point addition is not associative.
//
// The values are asserted bit-exactly (toBe / Assert.Equal without tolerance): this is an anti-cheat parity
// surface, so the two simulators must agree to the last bit. The leveled scenarios use integer base/perLevel so
// the backend's decimal `BaseAmount + AmountPerLevel × level` and the frontend's double arithmetic agree
// exactly; the `maxHealthDerivedAdditive` scenario lands an additive on a derived attribute (MaxHealth, which
// carries a static additive base) with a fractional base (perLevel 0, so no arithmetic divergence) to pin the
// additive accumulation order of the locked base relative to the static modifiers.
describe('class locked base (parity)', () => {
	type Allocation = [EAttribute, number];
	// [attribute, baseAmount, amountPerLevel]
	type DistributionSpec = [EAttribute, number, number];
	interface Scenario {
		allocations: Allocation[];
		distributions: DistributionSpec[];
		level: number;
		expected: [EAttribute, number][];
	}

	const scenarios: Record<string, Scenario> = {
		// Leveled base: the distribution scales with level. Strength = 0 (alloc) + (10 + 2 × 5) = 20.
		leveledBase: {
			allocations: [],
			distributions: [[EAttribute.Strength, 10, 2]],
			level: 5,
			expected: [[EAttribute.Strength, 20]]
		},

		// Locked base composes additively with the free-pool allocation. Endurance = 5 (alloc) + (4 + 3 × 2) = 15.
		basePlusAllocation: {
			allocations: [[EAttribute.Endurance, 5]],
			distributions: [[EAttribute.Endurance, 4, 3]],
			level: 2,
			expected: [[EAttribute.Endurance, 15]]
		},

		// Multiple attributes from the fingerprint, each landing on its own attribute. Strength = 3 + 0 × 4 = 3;
		// Agility = 7 + 1 × 4 = 11.
		multiAttribute: {
			allocations: [],
			distributions: [
				[EAttribute.Strength, 3, 0],
				[EAttribute.Agility, 7, 1]
			],
			level: 4,
			expected: [
				[EAttribute.Strength, 3],
				[EAttribute.Agility, 11]
			]
		},

		// Additive locked base on a DERIVED attribute (MaxHealth = 50 + 20·Endurance + 5·Strength). The locked
		// base feeds Endurance (34) and Strength (59) plus a fractional MaxHealth term (3.14, perLevel 0 so no
		// arithmetic divergence). MaxHealth carries a static additive base, so the locked-base additive must
		// accumulate in the same order relative to those statics on both sides: MaxHealth = 3.14 + 50 + 680 +
		// 295 = 1028.1399999999999, distinct from the "statics first" order (1028.14) past the 10th decimal.
		maxHealthDerivedAdditive: {
			allocations: [],
			distributions: [
				[EAttribute.Endurance, 34, 0],
				[EAttribute.Strength, 59, 0],
				[EAttribute.MaxHealth, 3.14, 0]
			],
			level: 1,
			expected: [[EAttribute.MaxHealth, 1028.1399999999999]]
		}
	};

	const makeAttrs = (...pairs: Allocation[]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

	const toDistributions = (specs: DistributionSpec[]): ClassAttributeDistribution[] =>
		specs.map(([attributeId, baseAmount, amountPerLevel]) => ({ attributeId, baseAmount, amountPerLevel }));

	it.each(Object.keys(scenarios))('composes %s class locked base onto attributes', (name) => {
		const { allocations, distributions, level, expected } = scenarios[name];
		// Compose through the production path: setData places the locked-base modifiers with the base set,
		// BEFORE the static engine modifiers, exactly as the backend's BattleSnapshot.GetModifiers +
		// AttributeCollection do. This is what makes the additive accumulation order identical across FE/BE.
		const ba = new BattleAttributes();
		ba.setData(makeAttrs(...allocations), true, classLockedBaseModifiers(toDistributions(distributions), level));

		for (const [attribute, value] of expected) {
			expect(ba.getValue(attribute)).toBe(value);
		}
	});
});
