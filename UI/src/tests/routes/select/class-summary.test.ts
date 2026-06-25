import { describe, it, expect } from 'vitest';
import { EEquipmentSlot, EModifierType, type IAttribute, type IClass, type IItem, type ISkill } from '$lib/api';
import { passiveSummary, resolveStarterEquipment, resolveStarterSkills } from '$routes/select/class-summary';

const cls = (overrides: Partial<IClass> = {}): IClass =>
	({
		id: 0,
		name: 'Warrior',
		description: '',
		word: 'kor',
		passiveAttributeId: 1,
		passiveAmount: 8,
		passiveScalingAmount: 0,
		passiveModifierType: EModifierType.Additive,
		starterSkillIds: [],
		starterEquipment: [],
		attributeDistributions: [],
		...overrides
	}) as IClass;

// Reference sets are index-addressable by id (the catalogue convention), so index === id here.
const skills = [
	{ id: 0, name: 'Punch' },
	{ id: 1, name: 'Slash' },
	{ id: 2, name: 'Fireball' }
] as unknown as ISkill[];

const items = [
	{ id: 0, name: 'Rags' },
	{ id: 1, name: 'Tunic' },
	{ id: 2, name: 'Iron Sword' }
] as unknown as IItem[];

const attributes = [
	{ id: 1, code: 'END' },
	{ id: 2, code: 'INT' }
] as unknown as IAttribute[];

describe('resolveStarterSkills', () => {
	it('resolves starter skill names in the class order', () => {
		const result = resolveStarterSkills(cls({ starterSkillIds: [2, 0] }), skills);
		expect(result).toEqual([
			{ id: 2, name: 'Fireball' },
			{ id: 0, name: 'Punch' }
		]);
	});

	it('degrades to a stable id label when the skills set is unavailable', () => {
		const result = resolveStarterSkills(cls({ starterSkillIds: [2] }), undefined);
		expect(result).toEqual([{ id: 2, name: 'Skill #2' }]);
	});
});

describe('resolveStarterEquipment', () => {
	it('lists equipment weapon-first with resolved item names', () => {
		const result = resolveStarterEquipment(
			cls({
				starterEquipment: [
					{ itemId: 1, equipmentSlot: EEquipmentSlot.ChestSlot },
					{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot }
				]
			}),
			items
		);
		// The weapon leads (it carries the kit's signature skill).
		expect(result.map((e) => e.name)).toEqual(['Iron Sword', 'Tunic']);
		expect(result[0].slot).toBe(EEquipmentSlot.WeaponSlot);
	});

	it('degrades to a stable id label when the items set is unavailable', () => {
		const result = resolveStarterEquipment(
			cls({ starterEquipment: [{ itemId: 7, equipmentSlot: EEquipmentSlot.WeaponSlot }] }),
			undefined
		);
		expect(result).toEqual([{ itemId: 7, slot: EEquipmentSlot.WeaponSlot, name: 'Item #7' }]);
	});
});

describe('passiveSummary', () => {
	it('renders a flat additive passive as a signed magnitude', () => {
		expect(passiveSummary(cls({ passiveAmount: 8 }), attributes)).toBe('+8 END');
	});

	it('renders a multiplicative passive with a ×factor', () => {
		const summary = passiveSummary(
			cls({ passiveModifierType: EModifierType.Multiplicative, passiveAmount: 1.1 }),
			attributes
		);
		expect(summary).toBe('×1.1 END');
	});

	it('appends the scaling clause for an attribute-scaled passive', () => {
		const summary = passiveSummary(cls({ passiveScalingAttributeId: 2, passiveScalingAmount: 0.5 }), attributes);
		expect(summary).toBe('+8 END (+0.5 per INT)');
	});

	it('omits the scaling clause when the scaling amount is zero', () => {
		const summary = passiveSummary(cls({ passiveScalingAttributeId: 2, passiveScalingAmount: 0 }), attributes);
		expect(summary).toBe('+8 END');
	});
});
