import { describe, it, expect } from 'vitest';
import { EItemCategory, ERarity, type IItem } from '$lib/api';
import { itemProficiencyRequirement, meetsItemProficiencyRequirement } from '$lib/common';

const item = (requiredProficiencyId?: number, requiredProficiencyLevel = 0): IItem =>
	({
		id: 1,
		name: 'Item',
		description: '',
		itemCategoryId: EItemCategory.Accessory,
		rarityId: ERarity.Common,
		iconPath: '',
		requiredProficiencyId,
		requiredProficiencyLevel,
		attributes: [],
		modSlots: [],
		tags: []
	}) as IItem;

// A simple per-proficiency level lookup; a missing id is the "never trained" state (level 0).
const levelOf = (levels: Record<number, number>) => (id: number) => levels[id] ?? 0;

describe('itemProficiencyRequirement', () => {
	it('returns undefined for an ungated item', () => {
		expect(itemProficiencyRequirement(item(), levelOf({}))).toBeUndefined();
	});

	it('resolves the requirement against the player level when gated', () => {
		const requirement = itemProficiencyRequirement(item(3, 5), levelOf({ 3: 4 }));
		expect(requirement).toEqual({ proficiencyId: 3, requiredLevel: 5, currentLevel: 4, met: false });
	});

	it('marks the requirement met once the player reaches the level', () => {
		expect(itemProficiencyRequirement(item(3, 5), levelOf({ 3: 5 }))?.met).toBe(true);
	});

	it('treats a never-trained proficiency as level 0', () => {
		const requirement = itemProficiencyRequirement(item(3, 1), levelOf({ 9: 99 }));
		expect(requirement).toMatchObject({ currentLevel: 0, met: false });
	});
});

describe('meetsItemProficiencyRequirement', () => {
	it('is always true for an ungated item', () => {
		expect(meetsItemProficiencyRequirement(item(), levelOf({}))).toBe(true);
	});

	it('is true exactly at the required level and above', () => {
		expect(meetsItemProficiencyRequirement(item(3, 5), levelOf({ 3: 5 }))).toBe(true);
		expect(meetsItemProficiencyRequirement(item(3, 5), levelOf({ 3: 6 }))).toBe(true);
	});

	it('is false below the required level', () => {
		expect(meetsItemProficiencyRequirement(item(3, 5), levelOf({ 3: 4 }))).toBe(false);
	});
});
