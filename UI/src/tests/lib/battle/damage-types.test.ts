import { describe, it, expect } from 'vitest';
import { EAttribute, EDamageType, EDamageTypeKey } from '$lib/api';
import {
	applies,
	attributesForKey,
	amplificationAttributes,
	resistanceAttributes,
	keyForAttribute
} from '$lib/battle/damage-types';

/* Mirror of the backend `DamageTypesTests` (spike #1320). Keep the scenarios row-for-row aligned with
   Game.Core.Tests/Attributes/DamageTypesTests.cs so the two simulators cannot drift. */

// The full taxonomy table (decision 3); key order is fixed and parity-critical.
const appliesCases: [EDamageType, EDamageTypeKey[]][] = [
	[EDamageType.Physical, [EDamageTypeKey.Physical]],
	[EDamageType.Fire, [EDamageTypeKey.Fire, EDamageTypeKey.Elemental]],
	[EDamageType.Water, [EDamageTypeKey.Water, EDamageTypeKey.Elemental]],
	[EDamageType.Earth, [EDamageTypeKey.Earth, EDamageTypeKey.Elemental]],
	[EDamageType.Wind, [EDamageTypeKey.Wind, EDamageTypeKey.Elemental]],
	[EDamageType.Bleed, [EDamageTypeKey.Bleed, EDamageTypeKey.Dot]],
	[EDamageType.Poison, [EDamageTypeKey.Poison, EDamageTypeKey.Dot]],
	[EDamageType.Burn, [EDamageTypeKey.Burn, EDamageTypeKey.Fire, EDamageTypeKey.Elemental, EDamageTypeKey.Dot]]
];

describe('damage-types applies()', () => {
	it.each(appliesCases)('returns the exact key set in fixed order for %s', (type, expected) => {
		expect(applies(type)).toEqual(expected);
	});

	it.each(appliesCases)("leads with the leaf type's own key for %s", (type, expected) => {
		expect(expected[0] as number).toBe(type as number);
		expect(applies(type)[0]).toBe(expected[0]);
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

	it.each(appliesCases)('amp/resist helpers track applies() for %s', (type, keys) => {
		expect(amplificationAttributes(type)).toEqual(keys.map((k) => attributesForKey(k).amplification));
		expect(resistanceAttributes(type)).toEqual(keys.map((k) => attributesForKey(k).resistance));
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
			EDamageTypeKey.Dot
		]) {
			const { amplification, resistance } = attributesForKey(type);
			expect(keyForAttribute(amplification)).toBe(type);
			expect(keyForAttribute(resistance)).toBe(type);
		}
	});

	it.each([EAttribute.Strength, EAttribute.Defense, EAttribute.DamageTakenPerSecond])(
		'returns undefined for non-amp/resist attribute %s',
		(attribute) => {
			expect(keyForAttribute(attribute)).toBeUndefined();
		}
	);
});
