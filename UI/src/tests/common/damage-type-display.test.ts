import { describe, it, expect } from 'vitest';
import { EDamageType, EDamageTypeKey } from '$lib/api';
import {
	damageTypeColor,
	damageTypeGlyph,
	damageTypeKeyColor,
	damageTypeKeyGlyph,
	damageTypeKeyName,
	damageTypeName
} from '../../lib/common/damage-type-display';

describe('damage-type-key helpers', () => {
	it('maps a key to its themeable --dmg-* accent', () => {
		expect(damageTypeKeyColor(EDamageTypeKey.Fire)).toBe('var(--dmg-fire)');
		expect(damageTypeKeyColor(EDamageTypeKey.Physical)).toBe('var(--dmg-physical)');
		expect(damageTypeKeyColor(EDamageTypeKey.Elemental)).toBe('var(--dmg-elemental)');
		expect(damageTypeKeyColor(EDamageTypeKey.Dot)).toBe('var(--dmg-dot)');
	});

	it('names the cross-cutting categories readably', () => {
		expect(damageTypeKeyName(EDamageTypeKey.Elemental)).toBe('Elemental');
		expect(damageTypeKeyName(EDamageTypeKey.Dot)).toBe('Damage Over Time');
		expect(damageTypeKeyName(EDamageTypeKey.Bleed)).toBe('Bleed');
	});

	it('maps a key to its glyph variant', () => {
		expect(damageTypeKeyGlyph(EDamageTypeKey.Water)).toBe('water');
		expect(damageTypeKeyGlyph(EDamageTypeKey.Dot)).toBe('dot');
	});
});

describe('leaf damage-type helpers', () => {
	it('maps a leaf type to its accent / name / glyph', () => {
		expect(damageTypeColor(EDamageType.Fire)).toBe('var(--dmg-fire)');
		expect(damageTypeName(EDamageType.Burn)).toBe('Burn');
		expect(damageTypeGlyph(EDamageType.Earth)).toBe('earth');
	});

	it('treats Physical as the neutral baseline', () => {
		expect(damageTypeColor(EDamageType.Physical)).toBe('var(--dmg-physical)');
		expect(damageTypeName(EDamageType.Physical)).toBe('Physical');
	});

	// The eight leaf EDamageType values share their numeric value with the matching EDamageTypeKey, so a
	// leaf type's metadata must equal its own category key's metadata — guarding the cast in `leafKey`.
	it('agrees with the matching damage-type key for every leaf type', () => {
		const leaves = [
			EDamageType.Physical,
			EDamageType.Fire,
			EDamageType.Water,
			EDamageType.Earth,
			EDamageType.Wind,
			EDamageType.Bleed,
			EDamageType.Poison,
			EDamageType.Burn
		];
		for (const type of leaves) {
			const key = type as unknown as EDamageTypeKey;
			expect(damageTypeColor(type)).toBe(damageTypeKeyColor(key));
			expect(damageTypeName(type)).toBe(damageTypeKeyName(key));
			expect(damageTypeGlyph(type)).toBe(damageTypeKeyGlyph(key));
		}
	});
});
