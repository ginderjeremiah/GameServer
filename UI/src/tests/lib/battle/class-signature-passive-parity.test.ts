import { describe, it, expect, vi } from 'vitest';
import { EAttribute, EModifierType, type IAttribute, type ISignaturePassive } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import {
	classLockedBaseModifiers,
	classSignaturePassiveModifier,
	type ClassAttributeDistribution
} from '$lib/battle/class-modifiers';

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

// Parity guard for the class signature-passive attribute pipeline (spike #1126 area E). Every scenario here
// MUST be mirrored — with identical inputs (the same allocations, class attribute distributions, level, and
// passive) and identical expected attribute values — in the backend suite
// Game.Core.Tests/Attributes/ClassSignaturePassiveParityTests.cs. The passive resolves to a single modifier —
// flat (`amount`) or attribute-scaled (`amount + scalingAmount × value(scalingAttribute)`) — composed into the
// battler LAST: after the free pool, the locked base, and the static engine modifiers, reading the
// fully-resolved value of its scaling attribute (snapshot state, so a V1 passive never depends on itself).
//
// The values are asserted bit-exactly (toBe / Assert.Equal without tolerance): this is an anti-cheat parity
// surface, so the two simulators must agree to the last bit. The backend's `ClassSignaturePassive.GetModifier`
// does the `amount + scalingAmount × value` arithmetic in double (each authored decimal operand cast first),
// matching this module — the `fractionalScaling` scenario pins the case (scalingAmount 0.1, source 3)
// decimal-then-cast would have diverged on (0.3 vs the double 0.30000000000000004).
describe('class signature passive (parity)', () => {
	type Allocation = [EAttribute, number];
	// [attribute, baseAmount, amountPerLevel]
	type DistributionSpec = [EAttribute, number, number];
	// [attribute, amount, scalingAttribute | null, scalingAmount, modifierType]
	type PassiveSpec = [EAttribute, number, EAttribute | null, number, EModifierType];
	interface Scenario {
		allocations: Allocation[];
		distributions: DistributionSpec[];
		level: number;
		passive: PassiveSpec;
		expected: [EAttribute, number][];
	}

	const scenarios: Record<string, Scenario> = {
		// Flat additive passive on a core attribute, on top of the free pool and the locked base.
		// Strength = 5 (alloc) + (3 + 1 × 2) locked base + 4 (passive) = 14.
		flatAdditiveCore: {
			allocations: [[EAttribute.Strength, 5]],
			distributions: [[EAttribute.Strength, 3, 1]],
			level: 2,
			passive: [EAttribute.Strength, 4, null, 0, EModifierType.Additive],
			expected: [[EAttribute.Strength, 14]]
		},

		// Passive scaling off a CORE attribute, landing on a DERIVED one.
		// Endurance = 5 (alloc) + (4 + 3 × 2) locked base = 15. Passive on Toughness = 2 + 0.5 × 15 = 9.5.
		// Toughness (static 2·Endurance) = 30 + 9.5 = 39.5 — the passive accumulates after the statics, the
		// same order on both sides.
		scaledOffCoreOntoDerived: {
			allocations: [[EAttribute.Endurance, 5]],
			distributions: [[EAttribute.Endurance, 4, 3]],
			level: 2,
			passive: [EAttribute.Toughness, 2, EAttribute.Endurance, 0.5, EModifierType.Additive],
			expected: [
				[EAttribute.Toughness, 39.5],
				[EAttribute.Endurance, 15]
			]
		},

		// Fractional scaling: Luck = 0 + 0.1 × Strength(3) = 0.30000000000000004 (double). The decimal-then-cast
		// path would produce 0.3, flagging the replay; both sides share double arithmetic, so it is exact.
		fractionalScaling: {
			allocations: [[EAttribute.Strength, 3]],
			distributions: [],
			level: 1,
			passive: [EAttribute.Luck, 0, EAttribute.Strength, 0.1, EModifierType.Additive],
			expected: [
				[EAttribute.Luck, 0.30000000000000004],
				[EAttribute.Strength, 3]
			]
		},

		// Passive scaling off a DERIVED attribute: it must read the scaling source's fully-assembled value
		// (statics included). MaxHealth = 50 + 20 × Endurance(2) + 5 × Strength(3) = 105. Luck = 0 + 0.5 × 105 = 52.5.
		scaledOffDerived: {
			allocations: [
				[EAttribute.Endurance, 2],
				[EAttribute.Strength, 3]
			],
			distributions: [],
			level: 1,
			passive: [EAttribute.Luck, 0, EAttribute.MaxHealth, 0.5, EModifierType.Additive],
			expected: [
				[EAttribute.Luck, 52.5],
				[EAttribute.MaxHealth, 105]
			]
		},

		// Self-scaling reads the PRE-passive value (snapshot state), never itself. Strength(pre) = 10 (alloc).
		// Passive = 0 + 0.5 × 10 = 5, baked once. Strength = 10 + 5 = 15.
		selfScaling: {
			allocations: [[EAttribute.Strength, 10]],
			distributions: [],
			level: 1,
			passive: [EAttribute.Strength, 0, EAttribute.Strength, 0.5, EModifierType.Additive],
			expected: [[EAttribute.Strength, 15]]
		},

		// Multiplicative passive applies AFTER the additive subtotal, the same order on both sides.
		// MaxHealth (static 50 + 20·Endurance(5)) = 150, then × 1.5 = 225.
		multiplicative: {
			allocations: [[EAttribute.Endurance, 5]],
			distributions: [],
			level: 1,
			passive: [EAttribute.MaxHealth, 1.5, null, 0, EModifierType.Multiplicative],
			expected: [
				[EAttribute.MaxHealth, 225],
				[EAttribute.Endurance, 5]
			]
		}
	};

	const makeAttrs = (...pairs: Allocation[]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

	const toDistributions = (specs: DistributionSpec[]): ClassAttributeDistribution[] =>
		specs.map(([attributeId, baseAmount, amountPerLevel]) => ({ attributeId, baseAmount, amountPerLevel }));

	const toPassive = ([
		attributeId,
		amount,
		scalingAttributeId,
		scalingAmount,
		modifierType
	]: PassiveSpec): ISignaturePassive => ({
		attributeId,
		amount,
		scalingAttributeId: scalingAttributeId ?? undefined,
		scalingAmount,
		modifierType
	});

	it.each(Object.keys(scenarios))('composes %s class signature passive onto attributes', (name) => {
		const { allocations, distributions, level, passive, expected } = scenarios[name];
		// Compose through the production path: the free pool + locked base + statics via setData, then the passive
		// added LAST (after the battler is built), reading its scaling source off the assembled attributes —
		// exactly as the backend's BattleSnapshot.ToBattler does. This is what makes the apply order identical.
		const ba = new BattleAttributes();
		ba.setData(makeAttrs(...allocations), true, classLockedBaseModifiers(toDistributions(distributions), level));
		ba.addModifier(classSignaturePassiveModifier(toPassive(passive), (attribute) => ba.getValue(attribute)));

		for (const [attribute, value] of expected) {
			expect(ba.getValue(attribute)).toBe(value);
		}
	});
});
