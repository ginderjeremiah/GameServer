import { describe, it, expect } from 'vitest';
import {
	attributeBonusSection,
	attributeDistributionSection
} from '$routes/admin/workbench/entities/attribute-table-sections';

/* Shared factories behind enemy/class's "stat distribution per level" table and item/item-mod's
   "flat stat bonus" table (#2061) — exercised against a minimal stand-in record rather than the
   real entity shapes, since the factories are entity-agnostic. */

interface Distributed {
	id: number;
	dist: { attributeId: number; baseAmount: number; amountPerLevel: number }[];
}

interface Bonused {
	id: number;
	bonuses: { attributeId: number; amount: number }[];
}

describe('attributeDistributionSection', () => {
	const section = attributeDistributionSection<Distributed>({
		key: 'attrs',
		itemsKey: 'dist',
		desc: 'Stat distribution per level',
		emptySub: 'No distribution yet.'
	});

	it('carries the configured key/desc/empty copy and the fixed baseAmount/amountPerLevel columns', () => {
		expect(section.key).toBe('attrs');
		expect(section.itemsKey).toBe('dist');
		expect(section.desc).toBe('Stat distribution per level');
		expect(section.emptySub).toBe('No distribution yet.');
		expect(section.columns.map((c) => c.key)).toEqual(['attributeId', 'baseAmount', 'amountPerLevel']);
	});

	it('counts and warns off the configured collection', () => {
		const empty: Distributed = { id: 0, dist: [] };
		const full: Distributed = { id: 0, dist: [{ attributeId: 0, baseAmount: 1, amountPerLevel: 0 }] };
		expect(section.count?.(empty)).toBe(0);
		expect(section.warn?.(empty)).toBe('No attribute distribution');
		expect(section.count?.(full)).toBe(1);
		expect(section.warn?.(full)).toBeNull();
	});

	it('newRow picks the first free attribute and zeroes both amounts', () => {
		// Strength (id 0) is already taken, so the first free attribute (id 1) is chosen.
		const rec: Distributed = { id: 0, dist: [{ attributeId: 0, baseAmount: 5, amountPerLevel: 2 }] };
		expect(section.newRow(rec)).toEqual({ attributeId: 1, baseAmount: 0, amountPerLevel: 0 });
	});
});

describe('attributeBonusSection', () => {
	const section = attributeBonusSection<Bonused>({
		itemsKey: 'bonuses',
		emptySub: 'No bonuses yet.'
	});

	it('always uses the "attributes" section key and the fixed amount column', () => {
		expect(section.key).toBe('attributes');
		expect(section.itemsKey).toBe('bonuses');
		expect(section.emptySub).toBe('No bonuses yet.');
		expect(section.columns.map((c) => c.key)).toEqual(['attributeId', 'amount']);
	});

	it('counts and warns off the configured collection', () => {
		const empty: Bonused = { id: 0, bonuses: [] };
		const full: Bonused = { id: 0, bonuses: [{ attributeId: 0, amount: 3 }] };
		expect(section.count?.(empty)).toBe(0);
		expect(section.warn?.(empty)).toBe('No attributes');
		expect(section.count?.(full)).toBe(1);
		expect(section.warn?.(full)).toBeNull();
	});

	it('newRow picks the first free attribute with a default amount of 1', () => {
		const rec: Bonused = { id: 0, bonuses: [{ attributeId: 0, amount: 5 }] };
		expect(section.newRow(rec)).toEqual({ attributeId: 1, amount: 1 });
	});
});
