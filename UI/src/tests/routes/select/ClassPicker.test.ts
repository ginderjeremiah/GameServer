import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';

// AttributeChip is stubbed to a countable marker so fingerprint rendering can be asserted without the
// icon/tooltip machinery.
vi.mock('$components/AttributeChip.svelte', () => ({ default: ChipStub }));
import ChipStub from '../game/screens/skills/AttributeChipStub.svelte';

import ClassPicker from '$routes/select/ClassPicker.svelte';
import { staticData } from '$stores/static-data.svelte';
import { EEquipmentSlot, EModifierType, type ICreatableClass } from '$lib/api';

const cls = (overrides: Partial<ICreatableClass> = {}): ICreatableClass =>
	({
		id: 0,
		name: 'Warrior',
		description: 'A frontline fighter.',
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

beforeEach(() => {
	staticData.attributes = [{ id: 1, code: 'END', name: 'Endurance' }] as unknown as NonNullable<
		typeof staticData.attributes
	>;
});

afterEach(() => {
	cleanup();
	staticData.attributes = undefined;
});

describe('ClassPicker', () => {
	it('renders nothing when there are no classes (hide-on-empty)', () => {
		render(ClassPicker, { classes: [], selectedClassId: null, onSelect: vi.fn() });
		expect(screen.queryByTestId('class-picker')).toBeNull();
	});

	it('renders an option per class', () => {
		const classes = [cls({ id: 0, name: 'Warrior' }), cls({ id: 1, name: 'Mage' })];
		render(ClassPicker, { classes, selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-option-0')).toBeTruthy();
		expect(screen.getByTestId('class-option-1')).toBeTruthy();
	});

	it('notifies the parent when an option is clicked', async () => {
		const classes = [cls({ id: 0, name: 'Warrior' }), cls({ id: 1, name: 'Mage' })];
		const onSelect = vi.fn();
		render(ClassPicker, { classes, selectedClassId: 0, onSelect });

		await fireEvent.click(screen.getByTestId('class-option-1'));
		expect(onSelect).toHaveBeenCalledWith(1);
	});

	it('previews the selected class kit: description, passive, skills, weapon-first equipment', () => {
		const classes = [
			cls({
				id: 0,
				description: 'A frontline fighter.',
				passiveAttributeId: 1,
				passiveAmount: 8,
				starterSkills: [
					{ id: 1, name: 'Slash' },
					{ id: 0, name: 'Punch' }
				],
				starterEquipment: [
					{ itemId: 0, equipmentSlot: EEquipmentSlot.ChestSlot, name: 'Rags' },
					{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot, name: 'Iron Sword' }
				],
				attributeDistributions: [
					{ attributeId: 1, baseAmount: 10, amountPerLevel: 1 },
					{ attributeId: 2, baseAmount: 4, amountPerLevel: 0 }
				]
			})
		];
		render(ClassPicker, { classes, selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-preview')).toBeTruthy();
		expect(screen.getByText('A frontline fighter.')).toBeTruthy();
		expect(screen.getByTestId('class-passive').textContent).toBe('+8 END');
		expect(screen.getByText('Slash')).toBeTruthy();
		expect(screen.getByText('Punch')).toBeTruthy();
		// Weapon leads the equipment list (its name renders).
		expect(screen.getByText('Iron Sword')).toBeTruthy();
		// A fingerprint chip per attribute distribution.
		expect(screen.getAllByTestId('attr-chip')).toHaveLength(2);
	});
});
