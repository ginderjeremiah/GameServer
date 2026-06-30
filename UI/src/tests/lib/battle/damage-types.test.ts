import { describe, it, expect } from 'vitest';
import { EAttribute, EDamageType, EDamageTypeKey } from '$lib/api';
import {
	applies,
	attributesForKey,
	amplificationAttributes,
	resistanceAttributes,
	isWeaponLeaf,
	keyForAttribute,
	dotAccumulators,
	dotTypeForAccumulator,
	primaryDamageType
} from '$lib/battle/damage-types';

/* Mirror of the backend `DamageTypesTests` (spike #1320). Keep the scenarios row-for-row aligned with
   Game.Core.Tests/Attributes/DamageTypesTests.cs so the two simulators cannot drift. */

// The full taxonomy table (decision 3, extended with the #1340 weapon leaves); key order is fixed and
// parity-critical. Each weapon leaf pulls its own key then the shared Physical key.
const appliesCases: [EDamageType, EDamageTypeKey[]][] = [
	[EDamageType.Physical, [EDamageTypeKey.Physical]],
	[EDamageType.Fire, [EDamageTypeKey.Fire, EDamageTypeKey.Elemental]],
	[EDamageType.Water, [EDamageTypeKey.Water, EDamageTypeKey.Elemental]],
	[EDamageType.Earth, [EDamageTypeKey.Earth, EDamageTypeKey.Elemental]],
	[EDamageType.Wind, [EDamageTypeKey.Wind, EDamageTypeKey.Elemental]],
	[EDamageType.Bleed, [EDamageTypeKey.Bleed, EDamageTypeKey.Dot]],
	[EDamageType.Poison, [EDamageTypeKey.Poison, EDamageTypeKey.Dot]],
	[EDamageType.Burn, [EDamageTypeKey.Burn, EDamageTypeKey.Fire, EDamageTypeKey.Elemental, EDamageTypeKey.Dot]],
	[EDamageType.Sword, [EDamageTypeKey.Sword, EDamageTypeKey.Physical]],
	[EDamageType.Axe, [EDamageTypeKey.Axe, EDamageTypeKey.Physical]],
	[EDamageType.Bow, [EDamageTypeKey.Bow, EDamageTypeKey.Physical]],
	[EDamageType.Club, [EDamageTypeKey.Club, EDamageTypeKey.Physical]],
	[EDamageType.Dagger, [EDamageTypeKey.Dagger, EDamageTypeKey.Physical]],
	[EDamageType.Unarmed, [EDamageTypeKey.Unarmed, EDamageTypeKey.Physical]]
];

describe('damage-types applies()', () => {
	it.each(appliesCases)('returns the exact key set in fixed order for %s', (type, expected) => {
		expect(applies(type)).toEqual(expected);
	});

	it.each(appliesCases)("leads with the leaf type's own key for %s", (type, expected) => {
		// Matched by enum name rather than ordinal — the weapon keys were appended out of leaf-ordinal
		// alignment (append-only), so the original types' coincidental ordinal match no longer holds.
		expect(EDamageTypeKey[expected[0] as number]).toBe(EDamageType[type as number]);
		expect(applies(type)[0]).toBe(expected[0]);
	});
});

// Mirror of the backend `DamageTypesTests` weapon-leaf cases (IsWeaponLeaf_*). The full set of leaf types is
// the keys of the appliesCases table above (every EDamageType).
const weaponLeafTypes = [
	EDamageType.Sword,
	EDamageType.Axe,
	EDamageType.Bow,
	EDamageType.Club,
	EDamageType.Dagger,
	EDamageType.Unarmed
];
const nonWeaponLeafTypes = [
	EDamageType.Physical,
	EDamageType.Fire,
	EDamageType.Water,
	EDamageType.Earth,
	EDamageType.Wind,
	EDamageType.Bleed,
	EDamageType.Poison,
	EDamageType.Burn
];

describe('damage-types isWeaponLeaf()', () => {
	it.each(weaponLeafTypes)('is true for weapon leaf %s', (type) => {
		expect(isWeaponLeaf(type)).toBe(true);
	});

	it.each(nonWeaponLeafTypes)('is false for non-weapon leaf %s', (type) => {
		// Generic Physical is the shared category key, not a weapon leaf; the elementals and DoT leaves don't
		// roll up under Physical at all — notably Burn ([Burn, Fire, Elemental, Dot]) excludes Physical.
		expect(isWeaponLeaf(type)).toBe(false);
	});

	it('agrees with the weapon-leaf set for every leaf type', () => {
		const weaponLeaves = new Set(weaponLeafTypes);
		for (const [type] of appliesCases) {
			expect(isWeaponLeaf(type)).toBe(weaponLeaves.has(type));
		}
	});
});

describe('damage-types attributesForKey()', () => {
	it.each([
		[EDamageTypeKey.Physical, EAttribute.PhysicalAmplification, EAttribute.PhysicalResistance],
		[EDamageTypeKey.Fire, EAttribute.FireAmplification, EAttribute.FireResistance],
		[EDamageTypeKey.Elemental, EAttribute.ElementalAmplification, EAttribute.ElementalResistance],
		[EDamageTypeKey.Dot, EAttribute.DotAmplification, EAttribute.DotResistance]
	])('returns the amp/resist pair for %s', (key, amp, resist) => {
		expect(attributesForKey(key)).toEqual({ amplification: amp, resistance: resist });
	});

	it.each([
		[EDamageTypeKey.Sword, EAttribute.SwordAmplification],
		[EDamageTypeKey.Axe, EAttribute.AxeAmplification],
		[EDamageTypeKey.Unarmed, EAttribute.UnarmedAmplification]
	])('returns an amplification-only pair (null resistance) for weapon key %s', (key, amp) => {
		expect(attributesForKey(key)).toEqual({ amplification: amp, resistance: null });
	});
});

describe('damage-types per-hit helpers', () => {
	it('maps Burn to its amplification attributes in order', () => {
		expect(amplificationAttributes(EDamageType.Burn)).toEqual([
			EAttribute.BurnAmplification,
			EAttribute.FireAmplification,
			EAttribute.ElementalAmplification,
			EAttribute.DotAmplification
		]);
	});

	it('maps Burn to its resistance attributes in order', () => {
		expect(resistanceAttributes(EDamageType.Burn)).toEqual([
			EAttribute.BurnResistance,
			EAttribute.FireResistance,
			EAttribute.ElementalResistance,
			EAttribute.DotResistance
		]);
	});

	it('maps Sword to weapon + physical amplification but physical-only resistance', () => {
		// The amplification-only weapon key contributes no resistance, so a weapon hit mitigates via the
		// shared Physical key alone (#1340).
		expect(amplificationAttributes(EDamageType.Sword)).toEqual([
			EAttribute.SwordAmplification,
			EAttribute.PhysicalAmplification
		]);
		expect(resistanceAttributes(EDamageType.Sword)).toEqual([EAttribute.PhysicalResistance]);
	});

	it.each(appliesCases)('amp/resist helpers track applies() for %s', (type, keys) => {
		expect(amplificationAttributes(type)).toEqual(keys.map((k) => attributesForKey(k).amplification));
		// Amplification-only keys (the weapon leaves) contribute no resistance, so they drop out.
		expect(resistanceAttributes(type)).toEqual(
			keys.map((k) => attributesForKey(k).resistance).filter((r) => r !== null)
		);
	});
});

describe('damage-types keyForAttribute()', () => {
	it('round-trips every amp and resist attribute', () => {
		for (const type of [
			EDamageTypeKey.Physical,
			EDamageTypeKey.Fire,
			EDamageTypeKey.Water,
			EDamageTypeKey.Earth,
			EDamageTypeKey.Wind,
			EDamageTypeKey.Bleed,
			EDamageTypeKey.Poison,
			EDamageTypeKey.Burn,
			EDamageTypeKey.Elemental,
			EDamageTypeKey.Dot,
			EDamageTypeKey.Sword,
			EDamageTypeKey.Axe,
			EDamageTypeKey.Bow,
			EDamageTypeKey.Club,
			EDamageTypeKey.Dagger,
			EDamageTypeKey.Unarmed
		]) {
			const { amplification, resistance } = attributesForKey(type);
			expect(keyForAttribute(amplification)).toBe(type);
			// A weapon key has no resistance attribute to round-trip.
			if (resistance !== null) {
				expect(keyForAttribute(resistance)).toBe(type);
			}
		}
	});

	it.each([EAttribute.Strength, EAttribute.Toughness, EAttribute.BleedDamagePerSecond])(
		'returns undefined for non-amp/resist attribute %s',
		(attribute) => {
			expect(keyForAttribute(attribute)).toBeUndefined();
		}
	);
});

describe('damage-types DoT accumulators (#1320 Area C)', () => {
	// The fixed iteration order the end-of-tick DoT phase folds the types in (a parity contract).
	const expectedAccumulators: [EDamageType, EAttribute][] = [
		[EDamageType.Bleed, EAttribute.BleedDamagePerSecond],
		[EDamageType.Poison, EAttribute.PoisonDamagePerSecond],
		[EDamageType.Burn, EAttribute.BurnDamagePerSecond]
	];

	it('lists the three DoT accumulators in fixed order', () => {
		expect(dotAccumulators().map((a) => [a.type, a.accumulator])).toEqual(expectedAccumulators);
	});

	it.each(expectedAccumulators)('round-trips %s through dotTypeForAccumulator', (type, accumulator) => {
		expect(dotTypeForAccumulator(accumulator)).toBe(type);
	});

	it.each([EAttribute.Strength, EAttribute.HealthRegenPerSecond, EAttribute.FireResistance])(
		'returns undefined for non-DoT-accumulator attribute %s',
		(attribute) => {
			expect(dotTypeForAccumulator(attribute)).toBeUndefined();
		}
	);
});

/* Mirror of the backend `SkillTests.PrimaryDamageType*` cases (spike #1343) — the two implementations must
   agree, since both feed the display surfaces and the interim single-type direct-hit call. */
describe('primaryDamageType', () => {
	it('returns the single portion type', () => {
		expect(primaryDamageType([{ type: EDamageType.Fire, weight: 1 }])).toBe(EDamageType.Fire);
	});

	it('picks the highest-weight portion', () => {
		expect(
			primaryDamageType([
				{ type: EDamageType.Physical, weight: 0.4 },
				{ type: EDamageType.Fire, weight: 0.6 }
			])
		).toBe(EDamageType.Fire);
	});

	it('on a weight tie picks the first authored portion', () => {
		expect(
			primaryDamageType([
				{ type: EDamageType.Water, weight: 1 },
				{ type: EDamageType.Fire, weight: 1 }
			])
		).toBe(EDamageType.Water);
	});

	it('falls back to Physical for an empty portion set', () => {
		expect(primaryDamageType([])).toBe(EDamageType.Physical);
	});
});
