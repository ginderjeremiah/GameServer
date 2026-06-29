import { describe, it, expect } from 'vitest';
import { EAttribute, EAttributeType, EDamageTypeKey } from '$lib/api';
import { groupAttributeOptions, filterAttributeGroups } from '$routes/admin/workbench/components/attribute-groups';
import type { SelectOption } from '$routes/admin/workbench/entities/types';
import { makeAttribute } from '../../../../fixtures/attributes';

// A reference set spanning every taxonomy band plus a slice of the damage-type amp/resist family,
// with display orders deliberately out of enum order so the displayOrder sort is exercised.
const ATTRIBUTES = [
	makeAttribute(EAttribute.Strength, 'Strength', { attributeType: EAttributeType.Primary, displayOrder: 0 }),
	makeAttribute(EAttribute.Endurance, 'Endurance', { attributeType: EAttributeType.Primary, displayOrder: 1 }),
	makeAttribute(EAttribute.MaxHealth, 'Max Health', { attributeType: EAttributeType.Secondary, displayOrder: 6 }),
	makeAttribute(EAttribute.HealthRegenPerSecond, 'Health Regen Per Second', {
		attributeType: EAttributeType.Status,
		displayOrder: 15
	}),
	makeAttribute(EAttribute.FireAmplification, 'Fire Amplification', {
		attributeType: EAttributeType.Affinity,
		displayOrder: 19,
		damageTypeKey: EDamageTypeKey.Fire
	}),
	makeAttribute(EAttribute.FireResistance, 'Fire Resistance', {
		attributeType: EAttributeType.Affinity,
		displayOrder: 20,
		damageTypeKey: EDamageTypeKey.Fire
	}),
	makeAttribute(EAttribute.PhysicalResistance, 'Physical Resistance', {
		attributeType: EAttributeType.Affinity,
		displayOrder: 18,
		damageTypeKey: EDamageTypeKey.Physical
	})
];

const option = (value: number, text: string): SelectOption => ({ value, text });

const allOptions = (): SelectOption[] => ATTRIBUTES.map((a) => option(a.id, a.name));

describe('groupAttributeOptions', () => {
	it('groups by taxonomy band in render order, splitting the affinity family by damage type', () => {
		const groups = groupAttributeOptions(allOptions(), ATTRIBUTES);
		expect(groups.map((g) => g.key)).toEqual([
			`type-${EAttributeType.Primary}`,
			`type-${EAttributeType.Secondary}`,
			`type-${EAttributeType.Status}`,
			// Affinity is split per damage-type key, ordered by key (Physical 0 before Fire 1).
			`affinity-${EDamageTypeKey.Physical}`,
			`affinity-${EDamageTypeKey.Fire}`
		]);
		expect(groups[0].label).toBe('Primary');
		expect(groups[0].options.map((o) => o.text)).toEqual(['Strength', 'Endurance']);
	});

	it('labels and tags damage-type sub-groups so the picker can tint/icon them', () => {
		const groups = groupAttributeOptions(allOptions(), ATTRIBUTES);
		const fire = groups.find((g) => g.key === `affinity-${EDamageTypeKey.Fire}`);
		expect(fire?.label).toBe('Fire');
		expect(fire?.damageTypeKey).toBe(EDamageTypeKey.Fire);
		expect(fire?.options.map((o) => o.text)).toEqual(['Fire Amplification', 'Fire Resistance']);
	});

	it('sorts within a band by the reference display order, not the incoming option order', () => {
		// Hand the options to the grouper in a scrambled order; the Fire sub-group must still sort
		// amplification (19) before resistance (20).
		const scrambled = [
			option(EAttribute.FireResistance, 'Fire Resistance'),
			option(EAttribute.FireAmplification, 'Fire Amplification')
		];
		const groups = groupAttributeOptions(scrambled, ATTRIBUTES);
		expect(groups[0].options.map((o) => o.value)).toEqual([EAttribute.FireAmplification, EAttribute.FireResistance]);
	});

	it('leads with a headerless group for sentinel options that are not real attributes', () => {
		const groups = groupAttributeOptions([option(-1, 'None'), ...allOptions()], ATTRIBUTES);
		expect(groups[0]).toEqual({ key: 'none', label: '', options: [option(-1, 'None')] });
	});

	it('falls back to a single flat group when the reference set is unavailable', () => {
		const opts = allOptions();
		expect(groupAttributeOptions(opts, undefined)).toEqual([{ key: 'all', label: '', options: opts }]);
		expect(groupAttributeOptions(opts, [])).toEqual([{ key: 'all', label: '', options: opts }]);
		expect(groupAttributeOptions([], undefined)).toEqual([]);
	});
});

describe('filterAttributeGroups', () => {
	it('filters options by case-insensitive text and drops emptied groups', () => {
		const groups = groupAttributeOptions(allOptions(), ATTRIBUTES);
		const filtered = filterAttributeGroups(groups, 'fire');
		expect(filtered.map((g) => g.key)).toEqual([`affinity-${EDamageTypeKey.Fire}`]);
		expect(filtered[0].options.map((o) => o.text)).toEqual(['Fire Amplification', 'Fire Resistance']);
	});

	it('returns the groups unchanged for an empty query', () => {
		const groups = groupAttributeOptions(allOptions(), ATTRIBUTES);
		expect(filterAttributeGroups(groups, '  ')).toBe(groups);
	});

	it('returns no groups when nothing matches', () => {
		const groups = groupAttributeOptions(allOptions(), ATTRIBUTES);
		expect(filterAttributeGroups(groups, 'zzz')).toEqual([]);
	});
});
