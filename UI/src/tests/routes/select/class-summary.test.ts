import { describe, it, expect } from 'vitest';
import {
	EEquipmentSlot,
	EModifierType,
	type IAttribute,
	type ICreatableClass,
	type ICreatableClassEquipment
} from '$lib/api';
import { passiveSummary, weaponFirst } from '$routes/select/class-summary';

const cls = (overrides: Partial<ICreatableClass> = {}): ICreatableClass =>
	({
		id: 0,
		name: 'Warrior',
		description: '',
		word: 'kor',
		passiveAttributeId: 1,
		passiveAmount: 8,
		passiveScalingAmount: 0,
		passiveModifierType: EModifierType.Additive,
		attributeDistributions: [],
		starterSkills: [],
		starterEquipment: [],
		...overrides
	}) as ICreatableClass;

const attributes = [
	{ id: 1, code: 'END' },
	{ id: 2, code: 'INT' }
] as unknown as IAttribute[];

describe('weaponFirst', () => {
	it('orders the weapon ahead of other equipment', () => {
		const equipment = [
			{ itemId: 1, equipmentSlot: EEquipmentSlot.ChestSlot, name: 'Tunic' },
			{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot, name: 'Iron Sword' }
		] as ICreatableClassEquipment[];

		expect(weaponFirst(equipment).map((e) => e.name)).toEqual(['Iron Sword', 'Tunic']);
	});

	it('does not mutate the input array', () => {
		const equipment = [
			{ itemId: 1, equipmentSlot: EEquipmentSlot.ChestSlot, name: 'Tunic' },
			{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot, name: 'Iron Sword' }
		] as ICreatableClassEquipment[];

		weaponFirst(equipment);
		expect(equipment.map((e) => e.name)).toEqual(['Tunic', 'Iron Sword']);
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

	it('falls back to the enum attribute name when reference data is unavailable', () => {
		// No attributes passed — the create-character screen may run before reference data loads.
		expect(passiveSummary(cls({ passiveAttributeId: 0, passiveAmount: 5 }))).toBe('+5 Strength');
	});
});
